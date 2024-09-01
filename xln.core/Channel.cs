using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading.Channels;
using Nethereum.Util;


namespace xln.core
{
  public class ChannelState
  {
    public XlnAddress Left { get; set; }
    public XlnAddress Right { get; set; }
    public string ChannelKey { get; set; }
    public string PreviousBlockHash { get; set; }
    public string PreviousStateHash { get; set; }
    public long BlockId { get; set; }
    public long Timestamp { get; set; }
    public long TransitionId { get; set; }
    public List<Subchannel> Subchannels { get; set; } = new List<Subchannel>();

    //public List<StoredSubcontract> Subcontracts { get; set; } = new List<StoredSubcontract>();

    public ChannelState()
    {
      Left = new XlnAddress();  // Предполагается, что у XlnAddress есть конструктор по умолчанию
      Right = new XlnAddress();
      ChannelKey = "0x0";
      PreviousBlockHash = "0x0";
      PreviousStateHash = "0x0";
      BlockId = 0;
      Timestamp = 0;
      TransitionId = 0;
      Subchannels = new List<Subchannel>();
      //Subcontracts = new List<StoredSubcontract>();
    }

    public ChannelState DeepClone()
    {
      return new ChannelState
      {
        Left = new XlnAddress(Left.ToString()), // Create a new XlnAddress with the same value
        Right = new XlnAddress(Right.ToString()),
        ChannelKey = ChannelKey,
        PreviousBlockHash = PreviousBlockHash,
        PreviousStateHash = PreviousStateHash,
        BlockId = BlockId,
        Timestamp = Timestamp,
        TransitionId = TransitionId,
        Subchannels = Subchannels?.Select(s => s.DeepClone()).ToList() // Assuming Subchannel has a DeepClone method
                                                                       //Subcontracts = Subcontracts?.Select(s => s.DeepClone()).ToList() // Uncomment if needed
      };
    }

    // Implement ICloneable interface
    public object Clone()
    {
      return DeepClone();
    }
  }

  public class Subchannel
  {
    public int ChainId { get; set; }
    public List<Delta> Deltas { get; set; } = new List<Delta>();
    public long CooperativeNonce { get; set; }
    public long DisputeNonce { get; set; }

    //public List<ProposedEvent> ProposedEvents { get; set; } = new List<ProposedEvent>();
    public bool ProposedEventsByLeft { get; set; }

    public Subchannel DeepClone()
    {
      return new Subchannel
      {
        ChainId = ChainId,
        Deltas = Deltas?.Select(d => d.DeepClone()).ToList(),
        CooperativeNonce = CooperativeNonce,
        DisputeNonce = DisputeNonce,
        //ProposedEvents = ProposedEvents?.Select(pe => pe.DeepClone()).ToList(), // Uncomment if needed
        ProposedEventsByLeft = ProposedEventsByLeft
      };
    }

    public object Clone()
    {
      return DeepClone();
    }
  }

  public class Delta
  {
    public int TokenId { get; set; }
    public BigInteger Collateral { get; set; }
    public BigInteger OnDelta { get; set; }
    public BigInteger OffDelta { get; set; }
    public BigInteger LeftCreditLimit { get; set; }
    public BigInteger RightCreditLimit { get; set; }
    public BigInteger LeftAllowance { get; set; }
    public BigInteger RightAllowance { get; set; }

    public Delta DeepClone()
    {
      return new Delta
      {
        TokenId = TokenId,
        Collateral = Collateral,
        OnDelta = OnDelta,
        OffDelta = OffDelta,
        LeftCreditLimit = LeftCreditLimit,
        RightCreditLimit = RightCreditLimit,
        LeftAllowance = LeftAllowance,
        RightAllowance = RightAllowance
      };
    }

    public object Clone()
    {
      return DeepClone();
    }
  }

  
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

    XlnAddress _ourAddress;
    XlnAddress _peerAddress;

    JobQueue _operationsQueue;

    Block _pendingBlock;

    User _owner;

    List<Transition> _mempool;

    ChannelState _dryRunState;
    ChannelState _state;

    //public ChannelState State { get; private set; }

    public Channel(User owner, XlnAddress ourAddress, XlnAddress peerAddress/*, ILogger<Channel> logger, IChannelStorage channelStorage*/)
    {
      _owner = owner;
      //_logger = logger;
      //_channelStorage = channelStorage;

      _ourAddress = ourAddress;
      _peerAddress = peerAddress;

      _state = new ChannelState();

      _state.Left = GetLeftAddress(ourAddress, peerAddress);
      _state.Right = GetRightAddress(ourAddress, peerAddress);
      _state.ChannelKey = CalculateChannelKey(_state.Left, _state.Right);

      EnsureValidAddressOrderOrThrow(_state.Left, _state.Right);

      _operationsQueue = new JobQueue();

      _pendingBlock = null;

      _mempool = new List<Transition>();
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
      string leftAsStr = left.ToString();
      string rightAsStr = right.ToString();

      // Убедимся, что адреса начинаются с "0x"
      string leftAddress = leftAsStr.ToString().StartsWith("0x") ? leftAsStr : "0x" + leftAsStr;
      string rightAddress = rightAsStr.StartsWith("0x") ? rightAsStr : "0x" + rightAsStr;

      // Упаковываем адреса
      byte[] packedData = new byte[64];
      Buffer.BlockCopy(HexToByteArray(leftAddress), 0, packedData, 0, 32);
      Buffer.BlockCopy(HexToByteArray(rightAddress), 0, packedData, 32, 32);

      // Присваиваем результат
      string channelKey = Keccak256.CalculateHashString(packedData);
      return channelKey;
    }

    private static byte[] HexToByteArray(string hex)
    {
      if (hex.StartsWith("0x"))
        hex = hex.Substring(2);

      byte[] bytes = new byte[hex.Length / 2];
      for (int i = 0; i < bytes.Length; i++)
      {
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
      }
      return bytes;
    }

    public void Recieve(FlushMessageBody msgBody)
    {
      _operationsQueue.EnqueueTask(new RecieveMessageTask(this, msgBody));
    }

    private async Task _queue_HandleRecieveAsync(FlushMessageBody msgBody)
    {
      ThrowIfNotValudFlushMessage(msgBody);

      // message body should containg peer signatures for the pending block
      ApplyPendingBlock(msgBody);

      ApplyNewBlock(msgBody);
    }

    void ThrowIfNotValudFlushMessage(FlushMessageBody msgBody)
    {
      //TODO check if message body valid
    }

    bool HasPendingBlock()
    {
      return (_pendingBlock != null);
    }

    void ApplyPendingBlock(FlushMessageBody msgBody)
    {
      // msgBody should not contain signatures or pending block should exist
      if (!HasPendingBlock())
        throw new InvalidOperationException();
    }

    void ApplyNewBlock(FlushMessageBody msgBody)
    {
      Block block = msgBody.Block;
      if (block != null)
      {
        Flush();
        return;
      }

      ThrowIfBlockInvalid(block);

      ApplyBlockDryRun(block);
      if(!VerifySignaturesDryRun(msgBody.NewSignatures))
      {
        throw new InvalidOperationException();
      }

      ApplyBlock(block);

      //CreateAndSaveHistoricalBlock();

      //TODO save channel state
      //Save();

      Flush();
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
      ApplyBlockToChannelState(_dryRunState, block);
    }

    private void ApplyBlock(Block block)
    {
      ApplyBlockToChannelState(_state, block);
    }

    private void ApplyBlockToChannelState(ChannelState channelState, Block block)
    {
      // Save previous hash first before changing the state
      channelState.PreviousStateHash = Keccak256.CalculateHashString(MessageSerializer.Encode(channelState));
      channelState.PreviousBlockHash = Keccak256.CalculateHashString(MessageSerializer.Encode(block));
      channelState.BlockId++;
      channelState.Timestamp = block.Timestamp;
      
      for (int i = 0; i < block.Transitions.Count(); i++)
      {
        ApplyTransitionToChannelState(channelState, block.Transitions[i]);
      }
    }

    private void ApplyTransitionToChannelState(ChannelState channelState, Transition transition)
    {
      try
      {
        transition.ApplyTo(channelState);
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

    private async Task _queue_HandleFlushAsync()
    {
      Body b = new Body(BodyTypes.kFlushMessage);
      await _owner.SendToAsync(_peerAddress, b, CancellationToken.None);
    }
    //public void ApplyTransition(ITransition transition)
    //{
    //  transition.Apply(this);
    //  SaveState();
    //}

    //private void SaveState()
    //{
    //  _channelStorage.SaveChannelState(State);
    //}

  }
}