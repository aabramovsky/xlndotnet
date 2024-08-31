using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace xln.core
{
  public class UserID
  {
    private readonly string _value;

    public UserID(string value)
    {
      _value = value;
    }

    public static implicit operator string(UserID userId) => userId._value;
    public static implicit operator UserID(string value) => new UserID(value);

    public override string ToString() => _value;
  }

  public class XlnAddress
  {
    private readonly string _value;

    public XlnAddress()
    {
      _value = "";
    }

    public XlnAddress(string value)
    {
      _value = value;
    }

    public static implicit operator string(XlnAddress userId) => userId._value;
    public static implicit operator XlnAddress(string value) => new XlnAddress(value);

    public override string ToString() => _value;

    public static bool operator <(XlnAddress left, XlnAddress right)
    {
      return string.Compare(left._value, right._value, StringComparison.Ordinal) < 0;
    }

    public static bool operator >(XlnAddress left, XlnAddress right)
    {
      return string.Compare(left._value, right._value, StringComparison.Ordinal) > 0;
    }

    public int CompareTo(XlnAddress other)
    {
      if (other == null) return 1;
      return string.Compare(_value, other._value, StringComparison.Ordinal);
    }
  }

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

  public class User
  {
    private WebSocketServer _server;
    ThreadSafeTransportStorage _transports;

    private UserID _myId;
    private XlnAddress _myAddress;

    private JobQueueManager _peerTaskQueues = new JobQueueManager();

    private ConcurrentDictionary<XlnAddress, Channel> _channelsByPeer = new ConcurrentDictionary<XlnAddress, Channel>();

    public User(UserID myId)
    {
      _server = new WebSocketServer();
      _server.OnClientConnected += _server_OnClientConnected;

      _transports = new ThreadSafeTransportStorage();

      _myId = myId;
      _myAddress = "myaddress";
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

    private async void SendTestMessage(ITransport transport)
    {
      var message = new Message
      {
        Header = new Header
        {
          From = "sender@example.com",
          To = "recipient@example.com"
        },
        Body = new Body(BodyTypes.kBroadcastProfile)
      };

      // Добавляем дополнительные свойства в Body
      message.Body.SetProperty("name", "John Doe");
      message.Body.SetProperty("age", 30);

      await transport.SendAsync(message, CancellationToken.None);
    }

    //public Channel GetOrCreateChannel(string address)
    //{
    //  // Логика получения существующего или создания нового Channel
    //}

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
        c.Recieve(msg.Body);
      }
    }

    public Channel GetOrCreateChannel(XlnAddress peerAddress)
    {
      // todo load channel state logic
      return _channelsByPeer.GetOrAdd(peerAddress, _ => new Channel(this, _myAddress, peerAddress));
    }

    public async Task SendToAsync(XlnAddress peerAddress, Body msgBody, CancellationToken ct)
    {
      //todo routing
      ITransport t = _transports.GetOrThrow(peerAddress);

      Message m = new Message();
      m.Body = msgBody;
      m.Header.From = _myAddress;
      m.Header.To = peerAddress;

      await t.SendAsync(m, ct);
    }
  }
 }
