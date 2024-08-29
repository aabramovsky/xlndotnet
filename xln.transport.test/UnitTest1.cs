//using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Net.WebSockets;
using System.Reflection.PortableExecutable;
using xln.core;

namespace xln.transport.test
{
  public class Tests
  {
    private WebSocketServer _server;
    private const string ServerUrl = "ws://localhost:9090/";
    private const string uriToListen = "http://localhost:9090/";
    private int _numClientsConnected = 0;

    [SetUp]
    public void Setup()
    {
      _server = new WebSocketServer();
    }

    [TearDown]
    public void TearDown()
    {
      _server.Stop();
    }

    [Test]
    public async Task ServerCanStartAndStopCorrectly()
    {
      _server.Start(uriToListen);
      await Task.Delay(1000);
      Assert.IsTrue(_server.IsRunning);      

      _server.Stop();
      await Task.Delay(1000);
      Assert.IsFalse(_server.IsRunning);
    }


    [Test]
    public async Task ServerCanAcceptOneClientConnection()
    {
      _server.Start(uriToListen);
      await Task.Delay(1000);

      var clientConnected = new TaskCompletionSource<bool>();
      _server.OnClientConnected += (sender, args) => clientConnected.SetResult(true);

      using (var client = new ClientWebSocket())
      {
        await client.ConnectAsync(new Uri(ServerUrl), CancellationToken.None);
        var result = await clientConnected.Task;
        Assert.IsTrue(result);
      }
    }

    [Test]
    public async Task ServerCanAcceptTenConnections()
    {
      _server.Start(uriToListen);
      await Task.Delay(1000);

      var connectedClients = 0;
      var allClientsConnected = new TaskCompletionSource<bool>();

      _server.OnClientConnected += (sender, args) =>
      {
        connectedClients++;
        if (connectedClients == 10)
        {
          allClientsConnected.SetResult(true);
        }
      };

      var clients = new List<ClientWebSocket>();
      for (int i = 0; i < 10; i++)
      {
        var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri(ServerUrl), CancellationToken.None);
        clients.Add(client);
      }

      var result = await allClientsConnected.Task;
      Assert.IsTrue(result);
      Assert.AreEqual(10, connectedClients);

      foreach (var client in clients)
      {
        client.Dispose();
      }
    }

    [Test]
    public async Task ClientCanConnectAndSendMessageViaITransport()
    {
      _server.Start(uriToListen);
      await Task.Delay(1000);

      var messageReceived = new TaskCompletionSource<xln.core.Message>();

      _server.OnClientConnected += async (sender, args) =>
      {
        var transport = args.Transport;
        var receivedMessage = await transport.ReceiveAsync(CancellationToken.None);
        messageReceived.SetResult(receivedMessage);
        transport.Dispose();
      };

      var client = await WebSocketClient.ConnectTo(new Uri(ServerUrl), "test", CancellationToken.None);

      var testMessage = new Message
      {
        Header = new Header { From = "TestClient", To = "TestServer" },
        Body = new Body(BodyTypes.kFlushMessage)
      };


      await client.SendAsync(testMessage, CancellationToken.None);

      var receivedMessage = await messageReceived.Task;
      Assert.IsNotNull(receivedMessage);
      Assert.AreEqual(new XlnAddress("TestClient"), receivedMessage.Header.From);
      Assert.AreEqual(new XlnAddress("TestServer"), receivedMessage.Header.To);
      Assert.AreEqual(BodyTypes.kFlushMessage, receivedMessage.Body.Type);

      await client.CloseAsync(CancellationToken.None);
      client.Dispose();
    }
  }
}