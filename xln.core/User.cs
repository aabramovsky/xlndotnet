using System;
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

    public XlnAddress(string value)
    {
      _value = value;
    }

    public static implicit operator string(XlnAddress userId) => userId._value;
    public static implicit operator XlnAddress(string value) => new XlnAddress(value);

    public override string ToString() => _value;
  }

  public class User
  {
    private const string _serverUrl = "ws://localhost:9090/";
    private const string _uriToListen = "http://localhost:9090/";

    private WebSocketServer _server;
    ThreadSafeTransportStorage _transports;

    private UserID _myId;

    private readonly object _lockTransports = new object();

    public User(UserID myId)
    {
      _server = new WebSocketServer();
      _server.OnClientConnected += _server_OnClientConnected;

      _transports = new ThreadSafeTransportStorage();

      _myId = myId;
    }

    private void _server_OnClientConnected(object? sender, ServerEventArgs e)
    {
      //TODO set real uri
      OnTransportCreated(e.xlnAddress, new Uri(""), e.Transport);
    }

    private void OnTransportCreated(XlnAddress xlnAddress, Uri uri, ITransport transport)
    {
      lock (_lockTransports)
      {
        if (!_transports.TryAdd(xlnAddress, transport))
          throw new Exception("transport for address already exists in ThreadSafeTransportStorage");

        _ = ListenTransportAsync(transport);
      }
    }

    public void StartServer(string uriToListen)
    {
      _server.Start(uriToListen);
    }

    public async void ConnectTo(Uri uri)
    {
      ITransport transport = await WebSocketClient.ConnectTo(uri, _myId, CancellationToken.None);

      //TODO set real xlnaddress
      OnTransportCreated(uri.ToString(), uri, transport);
    }

    //public Channel GetOrCreateChannel(string address)
    //{
    //  // Логика получения существующего или создания нового Channel
    //}

    private async Task ListenTransportAsync(ITransport transport)
    {
      try 
      {
        Message msg;
        do
        {
          msg = await transport.ReceiveAsync(CancellationToken.None);
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
  }
 }
