using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Signer.Crypto;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

namespace xln.core
{
  public class HandleIncomingMessageTask : ITask
  {
    private readonly Message _msg;
    private readonly User _user;
    public HandleIncomingMessageTask(User user, Message msg)
    {
      _user = user;
      _msg = msg;
    }

    public async Task ExecuteAsync()
    {
      await _user._queue_HandleIncomingMessageByPeerAsync(_msg);
    }
  }

  public class HashlockData
  {
    public long? OutTransitionId { get; set; }
    public XlnAddress? OutAddress { get; set; }
    public long? InTransitionId { get; set; }
    public XlnAddress? InAddress { get; set; }
    public string? Secret { get; set; }
  }

  public class DecryptedPackage
  {
    public XlnAddress? FinalRecipient { get; set; }
    public string? Secret { get; set; }
    public XlnAddress? NextHop { get; set; }
    public string? EncryptedPackage { get; set; }
  }

  public class User
  {
    private WebSocketServer _server;
    ThreadSafeTransportStorage _transports;

    private UserID _myId;
    private XlnAddress _myAddress;

    private JobQueueManager _peerTaskQueues = new JobQueueManager();

    private ConcurrentDictionary<XlnAddress, Channel> _channelsByPeer = new ConcurrentDictionary<XlnAddress, Channel>();

    private Dictionary<string, HashlockData> hashlockMap = new Dictionary<string, HashlockData>();

    private EthereumSigner _signer;


    public XlnAddress MyAddress { get { return _myAddress; } }

    public User(UserID myId)
    {
      _server = new WebSocketServer();
      _server.OnClientConnected += _server_OnClientConnected;

      _transports = new ThreadSafeTransportStorage();

      _myId = myId;
      _myAddress = "myaddress";

      _signer = new EthereumSigner("0x123456");
    }
    
    public string SignMessage(string message)
    {
      return _signer.SignMessage(message);
    }

    private void _server_OnClientConnected(object? sender, ServerEventArgs e)
    {
      //TODO set real uri
      OnTransportCreated(e.xlnAddress, e.Transport);
    }

    public void StartServer(string uriToListen)
    {
      _server.Start(uriToListen);
    }

    public async void ConnectTo(Uri uri)
    {
      ITransport transport = await WebSocketClient.ConnectTo(uri, _myId, CancellationToken.None);

      //TODO set real xlnaddress
      OnTransportCreated(uri.ToString(), transport);

      //SendTestMessage(transport);
    }

    private void OnTransportCreated(XlnAddress xlnAddress, ITransport transport)
    {
      
      if (!_transports.TryAdd(xlnAddress, transport))
        throw new Exception("transport for address already exists in ThreadSafeTransportStorage");

      _ = ListenTransportAsync(transport);
    }

    // each transport works in its own thread
    private async Task ListenTransportAsync(ITransport transport)
    {
      try 
      {
        Message msg;
        do
        {
          msg = await transport.ReceiveAsync(CancellationToken.None);
          _peerTaskQueues.EnqueueTask(msg.Header.From, new HandleIncomingMessageTask(this, msg));
        }
        while (transport.IsOpen);
      }
      catch (Exception ex) 
      { }
      finally 
      { 
        await transport.CloseAsync(CancellationToken.None);
        // todo notify transport closed
      }
    }

    // each peer has its own task queue and a separate thread for the queue where tasks are processed
    public async Task _queue_HandleIncomingMessageByPeerAsync(Message msg)
    {
      if(msg.Body.Type == BodyTypes.kFlushMessage) 
      {
        Channel c = GetOrCreateChannel(msg.Header.From);
        c.Recieve(msg.Body as FlushMessageBody);
      }
    }

    public Channel GetOrCreateChannel(XlnAddress peerAddress)
    {
      // todo load channel state logic
      return _channelsByPeer.GetOrAdd(peerAddress, _ => new Channel(this, peerAddress));
    }

    public Channel GetChannelOrThrow(XlnAddress peerAddress)
    {
      if (_channelsByPeer.TryGetValue(peerAddress, out Channel channel))
        return channel;
      
      throw new KeyNotFoundException($"No channel found for peer address: {peerAddress}");
    }

    public async Task SendToAsync(XlnAddress peerAddress, Body msgBody, CancellationToken ct)
    {
      //todo routing
      ITransport t = _transports.GetOrThrow(peerAddress);

      Message m = new Message();
      m.Body = msgBody;
      m.Header.From = MyAddress;
      m.Header.To = peerAddress;

      await t.SendAsync(m, ct);
    }

   
    
    public async Task ProcessAddPayment(Channel channel, StoredSubcontract storedSubcontract, bool isSender)
    {
      if(isSender)
      {
        await ProcessAddPaymentAsSender(channel, storedSubcontract);
      }
      else
      {
        await ProcessAddPaymentAsReciever(channel, storedSubcontract);
      }
    }

    private async Task ProcessAddPaymentAsSender(Channel channel, StoredSubcontract storedSubcontract)
    {
      AddPayment paymentTransition = ValidateAndExtractTransition<AddPayment>(storedSubcontract);

      string hashlock = paymentTransition.Hashlock;

      lock (hashlockMap)
      {
        HashlockData hashlockData = null;
        if (hashlockMap.TryGetValue(hashlock, out hashlockData))
        {
          // entry already exists, do general check
          if (hashlockData.OutAddress != channel.PeerAddress)
            throw new InvalidOperationException($"Fatal outAddress mismatch {hashlockData.OutAddress} {channel.PeerAddress}");

          hashlockData.OutTransitionId = storedSubcontract.TransitionId;
        }
        else
        {
          // create new entry for sender
          hashlockData = new HashlockData();
          hashlockData.OutTransitionId = storedSubcontract.TransitionId;
          hashlockData.OutAddress = channel.PeerAddress;

          hashlockMap[hashlock] = hashlockData;
        }
      }
    }

    private async Task ProcessAddPaymentAsReciever(Channel channel, StoredSubcontract storedSubcontract)
    {
      AddPayment payment = ValidateAndExtractTransition<AddPayment>(storedSubcontract);

      DerivedDelta derivedDelta = channel.State.DeriveDelta(
        payment.ChainId, payment.TokenId, channel.IsLeft
        );

      if (derivedDelta.InCapacity < payment.Amount)
        throw new InvalidOperationException($"Fatal Insufficient capacity {derivedDelta.InCapacity} for payment {payment.Amount} "); //{channel.State.ChannelId}


      DecryptedPackage decryptedPackage = await DecryptPackage<DecryptedPackage>(payment.EncryptedPackage);

      bool bFinalRecipient = (decryptedPackage.FinalRecipient == this.MyAddress);

      lock (hashlockMap)
      {
        HashlockData hashlockData = GetOrAddHashlockData(payment.Hashlock);
        hashlockData.InTransitionId = storedSubcontract.TransitionId;
        hashlockData.InAddress = channel.PeerAddress;
        if (!bFinalRecipient)
          hashlockData.OutAddress = decryptedPackage.NextHop;
      }

      if (bFinalRecipient)
      {
        await ProcessSettlePayment(channel, storedSubcontract, decryptedPackage.Secret, false);
      }
      else
      {
        // Intermediate node
        BigInteger fee = CalculateFee(payment.Amount);
        BigInteger forwardAmount = payment.Amount - fee;
        //TODO check if forwardAmount > 0 etc
        
        Channel nextHopChannel = GetChannelOrThrow(decryptedPackage.NextHop);

        var forwardPayment = new AddPayment(
            payment.ChainId,
            payment.TokenId,
            forwardAmount,
            payment.Hashlock,
            payment.Timelock,
            decryptedPackage.EncryptedPackage
        );


        nextHopChannel.AddToMempool(forwardPayment);
        nextHopChannel.Flush();
      }
    }

    public async Task ProcessSettlePayment(Channel channel, StoredSubcontract storedSubcontract, string secret, bool isSender)
    {
      AddPayment payment = ValidateAndExtractTransition<AddPayment>(storedSubcontract);

      if (isSender)
      {
        BigInteger fee = CalculateFee(payment.Amount);
        // TODO: Implement feesCollected logic
        // this.feesCollected += fee;
        // hub finalized their fee
        return;
      }

      
      lock (hashlockMap)
      {
        HashlockData paymentInfo;
        if (!hashlockMap.TryGetValue(payment.Hashlock, out paymentInfo))
          throw new InvalidOperationException("Fatal: No such paymentinfo");

        // looks like if(isReciever); WTF?!
        if (paymentInfo.InTransitionId.HasValue && paymentInfo.InAddress != null)
        {
          if (paymentInfo.Secret == null)
          {
            paymentInfo.Secret = secret;
            Console.WriteLine($"Settling payment to previous hop: {paymentInfo}");
            // should we double check payment?

            Channel peerChannel = GetChannelOrThrow(paymentInfo.InAddress);

            var settlePayment = new SettlePayment(paymentInfo.InTransitionId.Value, paymentInfo.Secret);
            peerChannel.AddToMempool(settlePayment);
            peerChannel.Flush();            
          }
        }
        else
        {
          paymentInfo.Secret = secret;
          Console.WriteLine($"Payment is now settled {channel.State.ChannelKey}", paymentInfo);
          // TODO: Implement resolve callback logic
        }
      }
    }

    private HashlockData GetOrAddHashlockData(string hashlock)
    {
      lock (hashlockMap)
      {
        if (!hashlockMap.TryGetValue(hashlock, out var hashlockData))
        {
          hashlockData = new HashlockData();
          hashlockMap[hashlock] = hashlockData;
        }
        return hashlockData;
      }
    }

    private static T ValidateAndExtractTransition<T>(StoredSubcontract storedSubcontract) where T : Transition
    {
      if (storedSubcontract == null)
        throw new ArgumentNullException(nameof(storedSubcontract), "storedSubcontract is null");

      if (storedSubcontract.OriginalTransition is not T transition)
        throw new InvalidOperationException($"OriginalTransition is not of type {typeof(T).Name}");

      return transition;
    }

    private BigInteger CalculateFee(BigInteger amount)
    {
      return BigInteger.Zero;
    }

    public async Task<T> DecryptPackage<T>(string encryptedPackage)
    {
      return MessageSerializer.DecodeFromString<T>(encryptedPackage);
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
  }
}
