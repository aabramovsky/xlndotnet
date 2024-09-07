using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading.Channels;
using Nethereum.Util;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;

namespace xln.core
{
  public class Channel
  {
    private abstract class ChannelTask : ITask
    {
      protected readonly FlushMessageBody _msgBody;
      protected readonly Channel _channel;
      protected ChannelTask(Channel channel, FlushMessageBody msgBody)
      {
        _channel = channel;
        _msgBody = msgBody;
      }

      public abstract Task ExecuteAsync();
    }

    private class RecieveMessageTask : ChannelTask
    {
      public RecieveMessageTask(Channel channel, FlushMessageBody msgBody)
        : base(channel, msgBody)
      { }

      public override async Task ExecuteAsync()
      {
        await _channel._queue_HandleRecieveAsync(_msgBody);
      }
    }

    private class FlushTask : ChannelTask
    {
      public FlushTask(Channel channel)
        : base(channel, null)
      { }

      public override async Task ExecuteAsync()
      {
        await _channel._queue_HandleFlushAsync();
      }
    }

    
    //private readonly ILogger<Channel> _logger;
    //private readonly IChannelStorage _channelStorage;

    //XlnAddress _ourAddress;
    XlnAddress _peerAddress;

    JobQueue _operationsQueue;

    Block _pendingBlock;

    User _owner;

    List<Transition> _mempool;

    ChannelState _dryRunState;
    ChannelState _state;
    bool _isLeft;

    public ChannelState State { get { return _state; } private set { _state = value; } }
    public ChannelState DryRunState { get { return _dryRunState; } private set { _dryRunState = value; } }
    public User Owner {  get { return _owner; } }
    public XlnAddress PeerAddress {  get { return _peerAddress; } }

    public bool IsLeft { get { return _isLeft; } }

    private long SentTransitions { get; set; } = 0;

    private Block? PendingBlock { get { return _pendingBlock; } set { _pendingBlock = value; } }
    private List<string> PendingSignatures { get; set; }
    private long SendCounter { get; set; }
    public string ChannelId { get { return Owner.MyAddress.ToString() + ":" + PeerAddress.ToString(); } }

    public Channel(User owner, XlnAddress peerAddress/*, ILogger<Channel> logger, IChannelStorage channelStorage*/)
    {
      _owner = owner;
      //_logger = logger;
      //_channelStorage = channelStorage;

      //_ourAddress = ourAddress;
      _peerAddress = peerAddress;

      _state = new ChannelState();

      _state.Left = GetLeftAddress(owner.MyAddress, peerAddress);
      _state.Right = GetRightAddress(owner.MyAddress, peerAddress);
      _state.ChannelKey = CalculateChannelKey(_state.Left, _state.Right);

      EnsureValidAddressOrderOrThrow(_state.Left, _state.Right);

      _operationsQueue = new JobQueue();

      _pendingBlock = null;

      _mempool = new List<Transition>();

      _isLeft = (owner.MyAddress == State.Left);

      SentTransitions = 0;

      SendCounter = 0;
    }

    public static XlnAddress GetLeftAddress(XlnAddress addr1, XlnAddress addr2)
    {
      return addr1 < addr2 ? addr1 : addr2;
    }

    public static XlnAddress GetRightAddress(XlnAddress addr1, XlnAddress addr2)
    {
      return addr1 < addr2 ? addr2 : addr1;
    }

    public static void EnsureValidAddressOrderOrThrow(XlnAddress left, XlnAddress right)
    {
      string leftAddress = left.ToString();
      string rightAddress = right.ToString();

      if (string.IsNullOrWhiteSpace(leftAddress) || string.IsNullOrWhiteSpace(rightAddress))
      {
        throw new ArgumentNullException(nameof(leftAddress), "Addresses cannot be null or empty.");
      }

      // Убедимся, что адреса начинаются с "0x"
      leftAddress = leftAddress.StartsWith("0x") ? leftAddress : "0x" + leftAddress;
      rightAddress = rightAddress.StartsWith("0x") ? rightAddress : "0x" + rightAddress;

      int comparisonResult = string.Compare(leftAddress, rightAddress, StringComparison.OrdinalIgnoreCase);

      if (comparisonResult == 0)
      {
        throw new ArgumentException("Addresses cannot be equal.", nameof(leftAddress));
      }

      if (comparisonResult > 0)
      {
        throw new ArgumentException("Left address must be less than right address.", nameof(leftAddress));
      }
    }

    public static string CalculateChannelKey(XlnAddress left, XlnAddress right)
    {
      // Упаковываем адреса
      byte[] packedData = new byte[64];
      Buffer.BlockCopy(Keccak256.HexToByteArray(left.ToString()), 0, packedData, 0, 32);
      Buffer.BlockCopy(Keccak256.HexToByteArray(right.ToString()), 0, packedData, 32, 32);

      // Присваиваем результат
      string channelKey = Keccak256.CalculateHashString(packedData);
      return channelKey;
    }
    
    public void Recieve(FlushMessageBody msgBody)
    {
      _operationsQueue.EnqueueTask(new RecieveMessageTask(this, msgBody));
    }

    //todo any function here can throw. How exactly should we handle these errors?
    private async Task _queue_HandleRecieveAsync(FlushMessageBody msgBody)
    {
      ThrowIfNotValudFlushMessage(msgBody);

      // message body should containg peer signatures for the pending block
      // or pending block should not exists
      ApplyPendingBlock(msgBody);

      ApplyNewBlock(msgBody);

      Flush(); // flush transitions in mempool if any
    }

    void ThrowIfNotValudFlushMessage(FlushMessageBody msgBody)
    {
      //TODO check if message body valid
    }

    void ApplyPendingBlock(FlushMessageBody msgBody)
    {
      // msgBody should not contain signatures or pending block should exist
      if (!HasPendingBlock())
        throw new InvalidOperationException();
    }

    bool HasPendingBlock()
    {
      return (_pendingBlock != null);
    }

    void ApplyNewBlock(FlushMessageBody msgBody)
    {
      Block block = msgBody.Block;

      ValidateBlockAndSignaturesOrThrow(block, msgBody.NewSignatures);

      ApplyBlock(block);

      //CreateAndSaveHistoricalBlock();

      //TODO save channel state
      //Save();
    }

    void ValidateBlockAndSignaturesOrThrow(Block block, List<string> signatures)
    {
      ThrowIfBlockInvalid(block);

      ApplyBlockDryRun(block);

      if (!VerifySignaturesDryRun(signatures))
        throw new InvalidOperationException();
    }

    void ThrowIfBlockInvalid(Block block)
    {
      //if (block.previousStateHash != keccak256(encode(this.state)))
      //{
      //  this.logger.log('fatal prevhashstate', stringify(debugState), block, stringify(this.state), this.data.pendingBlock);
      //  throw new Error(`Invalid previousStateHash: ${ this.ctx.user.toTag() } ${ block.previousStateHash} ${ debugState.blockId}
      //  vs ${ this.state.blockId}`);
      //}

      //if (block.previousBlockHash != this.state.previousBlockHash)
      //{
      //  this.logger.log('fatal prevhashblock', debugState, this.state);
      //  throw new Error('Invalid previousBlockHash');
      //}

      //if (block.isLeft == this.isLeft)
      //{
      //  throw new Error('Invalid isLeft');
      //}
    }

    private void ApplyBlockDryRun(Block block)
    {
      _dryRunState = _state.DeepClone();

      ApplyBlockToChannelState(block, true);
    }

    private void ApplyBlock(Block block)
    {
      ApplyBlockToChannelState(block, false);
    }

    private void ApplyBlockToChannelState(Block block, bool bDryRun)
    {
      ChannelState channelState = bDryRun ? _dryRunState : _state;

      // Save previous hash first before changing the state
      channelState.PreviousStateHash = Keccak256.CalculateHashString(MessageSerializer.Encode(channelState));
      channelState.PreviousBlockHash = Keccak256.CalculateHashString(MessageSerializer.Encode(block));
      channelState.BlockId++;
      channelState.Timestamp = block.Timestamp;
      
      for (int i = 0; i < block.Transitions.Count(); i++)
      {
        ApplyTransitionToChannelState(block.Transitions[i], block, bDryRun);
      }
    }

    private void ApplyTransitionToChannelState(Transition transition, Block block, bool bDryRun)
    {
      try
      {
        transition.ApplyTo(this, block, bDryRun);
      }
      catch (Exception e)
      {
        Console.WriteLine($"Error in ApplyTransition: {e.Message}");
        throw;
      }
    }

    private bool VerifySignaturesDryRun(List<string> signatures)
    {
      return VerifySignaturesOnState(_dryRunState, signatures);
    }

    private bool VerifySignatures(List<string> signatures)
    {
      return VerifySignaturesOnState(_state, signatures);
    }

    private bool VerifySignaturesOnState(ChannelState channelState, List<string> signatures)
    {
      return false;
    }

    public void Flush()
    {
      //  TODO should we check if _mempool has transitions and move them to FlushTask here?
      _operationsQueue.EnqueueTask(new FlushTask(this));
    }

    //TODO переписать всю эту ебану срань к хуям
    private async Task _queue_HandleFlushAsync()
    {
      //todo мы ведь не должны вызывать флуш когда ожидаем что отправленный блок применит другая сторона?!
      if (SentTransitions > 0)
      {
        Console.WriteLine($"Already flushing {ChannelId} blockid {State.BlockId}");
        return;
      }


      FlushMessageBody body = new FlushMessageBody(State.BlockId, new List<string>());

      // signed before block is applied
      var initialProofs = GetSubchannelProofs(State);
      
      body.PendingSignatures = initialProofs.Signatures;
      
      if (body.PendingSignatures.Count != State.Subchannels.Count + 1)
        throw new InvalidOperationException("Fatal: Invalid pending signatures length");

      // flush may or may not include new block
      if (_mempool.Count > 0)
      {
        const int BLOCK_LIMIT = 10;
        var transitions = _mempool.Take(BLOCK_LIMIT).ToList();
        var previousState = State.DeepClone();
        
        var block = new Block
        {
          IsLeft = this.IsLeft,
          Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
          PreviousStateHash = Keccak256.CalculateHashString(MessageSerializer.Encode(previousState)),
          PreviousBlockHash = State.PreviousBlockHash,
          BlockId = State.BlockId,
          Transitions = transitions
        };

        ApplyBlockDryRun(block);
        int expectedLength = DryRunState.Subchannels.Count + 1;

        PendingBlock = block.DeepClone();
        SentTransitions = transitions.Count;

        // signed after block is applied
        body.NewSignatures = GetSubchannelProofs(DryRunState).Signatures;
        if (expectedLength != body.NewSignatures.Count)
          throw new InvalidOperationException("Invalid signature length");
        
        PendingSignatures = body.NewSignatures;

        body.Block = block;
        if (body.NewSignatures.Count != expectedLength)
          throw new InvalidOperationException("Fatal: Invalid pending signatures length");
      }

      body.Counter = ++SendCounter;

      await _owner.SendToAsync(PeerAddress, body, CancellationToken.None);
    }
    
    public void AddToMempool(Transition tr)
    {
      lock(_mempool)
      {
        _mempool.Add(tr);
      }
    }

    //TODO переписать всю эту ебану срань к хуям
    public SubchannelProofs GetSubchannelProofs(ChannelState state)
    {      
      var encodedProofBody = new List<string>();
      var proofHash = new List<string>();
      var signatures = new List<string>();
      var subcontractBatch = new List<SubcontractProviderBatch>();
      var proofBody = new List<ProofBody>();

      for (int i = 0; i < state.Subchannels.Count; i++)
      {
        var subchannel = state.Subchannels[i];
        proofBody.Add(new ProofBody
        {
          OffDeltas = subchannel.Deltas.Select(d => d.OffDelta).ToList(),
          TokenIds = subchannel.Deltas.Select(d => d.TokenId).ToList(),
          Subcontracts = new List<SubcontractInfo>()
        });

        subcontractBatch.Add(new SubcontractProviderBatch
        {
          Payment = new List<PaymentSubcontract>(),
          Swap = new List<SwapSubcontract>()
        });
      }

      foreach (var storedSubcontract in state.Subcontracts)
      {
        if (storedSubcontract.OriginalTransition is AddPayment addPayment)
        {
          ProcessAddPaymentSubcontract(state, subcontractBatch, addPayment);
        }
        else if (storedSubcontract.OriginalTransition is AddSwap addSwap)
        {
          ProcessAddSwapSubcontract(state, subcontractBatch, addSwap);
        }
      }

      for (int i = 0; i < state.Subchannels.Count; i++)
      {
        var subchannel = state.Subchannels[i];
        var encodedBatch = AbiEncode(AbiDefinitions.SubcontractBatchABI, subcontractBatch[i]);

        proofBody[i].Subcontracts.Add(new SubcontractInfo
        {
          SubcontractProviderAddress = ENV.SubcontractProviderAddress,
          EncodedBatch = encodedBatch,
          Allowances = new List<object>() // Implement allowance logic if needed
        });

        encodedProofBody.Add(AbiEncode(AbiDefinitions.ProofbodyABI, proofBody[i]));

        var fullProof = new object[]
        {
                    (int)MessageType.DisputeProof,
                    state.ChannelKey,
                    subchannel.CooperativeNonce,
                    subchannel.DisputeNonce,
                    Keccak256.CalculateHash(encodedProofBody[i].HexToByteArray())
        };

        var encodedMsg = AbiEncode(
            new[] { "uint8", "bytes", "uint", "uint", "bytes32" },
            fullProof
        );

        proofHash.Add(Keccak256.CalculateHashString(encodedMsg.HexToByteArray()));
        signatures.Add(Owner.SignMessage(proofHash[i]));
      }

      // Add global state signature on top
      signatures.Add(Owner.SignMessage(Keccak256.CalculateHashString(MessageSerializer.Encode(state))));

      return new SubchannelProofs
      {
        EncodedProofBody = encodedProofBody,
        SubcontractBatch = subcontractBatch,
        ProofBody = proofBody,
        ProofHash = proofHash,
        Signatures = signatures
      };
    }

    private void ProcessAddPaymentSubcontract(ChannelState state, List<SubcontractProviderBatch> subcontractBatch, AddPayment addPayment)
    {
      var subchannelIndex = state.Subchannels.FindIndex(s => s.ChainId == addPayment.ChainId);
      if (subchannelIndex < 0)
      {
        throw new InvalidOperationException($"Subchannel with chainId {addPayment.ChainId} not found.");
      }

      var deltaIndex = state.Subchannels[subchannelIndex].Deltas.FindIndex(d => d.TokenId == addPayment.TokenId);
      if (deltaIndex < 0)
      {
        throw new InvalidOperationException($"Delta with tokenId {addPayment.TokenId} not found.");
      }

      subcontractBatch[subchannelIndex].Payment.Add(new PaymentSubcontract
      {
        DeltaIndex = deltaIndex,
        Amount = addPayment.Amount,
        RevealedUntilBlock = addPayment.Timelock,
        Hash = addPayment.Hashlock
      });
    }

    private void ProcessAddSwapSubcontract(ChannelState state, List<SubcontractProviderBatch> subcontractBatch, AddSwap addSwap)
    {
      var subchannelIndex = state.Subchannels.FindIndex(s => s.ChainId == addSwap.ChainId);
      if (subchannelIndex < 0)
      {
        throw new InvalidOperationException($"Subchannel with chainId {addSwap.ChainId} not found.");
      }

      var deltaIndex = state.Subchannels[subchannelIndex].Deltas.FindIndex(d => d.TokenId == addSwap.TokenId);
      if (deltaIndex < 0)
      {
        throw new InvalidOperationException($"Delta with tokenId {addSwap.TokenId} not found.");
      }

      var subTokenIndex = state.Subchannels[subchannelIndex].Deltas.FindIndex(d => d.TokenId == addSwap.SubTokenId);
      if (subTokenIndex < 0)
      {
        throw new InvalidOperationException($"Delta with subTokenId {addSwap.SubTokenId} not found.");
      }

      subcontractBatch[subchannelIndex].Swap.Add(new SwapSubcontract
      {
        OwnerIsLeft = addSwap.OwnerIsLeft,
        AddDeltaIndex = deltaIndex,
        AddAmount = addSwap.AddAmount,
        SubDeltaIndex = subTokenIndex,
        SubAmount = addSwap.SubAmount
      });
    }

    private string AbiEncode(string[] types, params object[] values)
    {
      var abiEncoder = new ABIEncode();
      return abiEncoder.GetABIEncoded(types, values).ToHex();
    }
  }
}