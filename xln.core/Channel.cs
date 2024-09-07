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

      _dryRunState = _state.DeepClone();

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

    public void AddToMempool(Transition tr)
    {
      lock(_mempool)
      {
        _mempool.Add(tr);
      }
    }
  }
}