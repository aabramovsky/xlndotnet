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
  }

  public class Subchannel
  {
    public int ChainId { get; set; }
    public List<Delta> Deltas { get; set; } = new List<Delta>();
    public long CooperativeNonce { get; set; }
    public long DisputeNonce { get; set; }

    //public List<ProposedEvent> ProposedEvents { get; set; } = new List<ProposedEvent>();
    public bool ProposedEventsByLeft { get; set; }
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
  }

  

  public class SendMessageTask : ITask
  {
    private readonly Message _msg;
    private readonly Channel _channel;
    public SendMessageTask(Channel channel, Message msg)
    {
      _channel = channel;
      _msg = msg;
    }

    public async Task ExecuteAsync()
    {
      //await _channel._queue_HandleSend(_msg.Body);
    }
  }


  public class Channel
  {
    private abstract class ChannelTask : ITask
    {
      protected readonly Body _msgBody;
      protected readonly Channel _channel;
      protected ChannelTask(Channel channel, Body msgBody)
      {
        _channel = channel;
        _msgBody = msgBody;
      }

      public abstract Task ExecuteAsync();
    }

    private class RecieveMessageTask : ChannelTask
    {
      public RecieveMessageTask(Channel channel, Body msgBody)
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

    User _owner;

    //public static string MakeChannelId(XlnAddress leftAddress, XlnAddress rightAddress)
    //{
    //  return (leftAddress.ToString() + ":" + rightAddress.ToString());
    //}

    public ChannelState State { get; private set; }

    public Channel(User owner, XlnAddress ourAddress, XlnAddress peerAddress/*, ILogger<Channel> logger, IChannelStorage channelStorage*/)
    {
      _owner = owner;
      //_logger = logger;
      //_channelStorage = channelStorage;

      _ourAddress = ourAddress;
      _peerAddress = peerAddress;

      State = new ChannelState();
      
      State.Left = GetLeftAddress(ourAddress, peerAddress);
      State.Right = GetRightAddress(ourAddress, peerAddress);
      State.ChannelKey = CalculateChannelKey(State.Left, State.Right);

      EnsureValidAddressOrderOrThrow(State.Left, State.Right);

      _operationsQueue = new JobQueue();
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

      // Вычисляем Keccak256 хеш
      byte[] hashBytes = Sha3Keccack.Current.CalculateHash(packedData);

      // Присваиваем результат
      string channelKey = "0x" + BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); ;
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

    public void Recieve(Body msgBody)
    {
      _operationsQueue.EnqueueTask(new RecieveMessageTask(this, msgBody));
    }

    private async Task _queue_HandleRecieveAsync(Body msgBody)
    {

    }

    public void Flush()
    {
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