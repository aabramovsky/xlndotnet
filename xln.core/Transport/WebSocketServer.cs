using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace xln.core
{
  public class WebSocketServer : Server
  {
    private HttpListener _listener = null;
    private CancellationTokenSource _cts = null;

    public override void Start(string uriToListen)
    {
      ListenerAddress = uriToListen;
      Task.Run(() => StartAsync(uriToListen));
    }

    public override void Stop()
    {
      StopAsync().Wait();
    }

    protected async Task StartAsync(string uriToListen)
    {
      _cts = new CancellationTokenSource();
      _listener = new HttpListener();
      _listener.Prefixes.Add(uriToListen);
      _listener.Start();

      //Console.WriteLine("WebSocket server started on {uriToListen}");

      this.IsRunning = true;

      while (!_cts.IsCancellationRequested)
      {
        try
        {
          var context = await _listener.GetContextAsync();
          if (context.Request.IsWebSocketRequest)
          {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            RaiseOnClientConnected(new WebSocketTransport(webSocketContext.WebSocket));
            //var clientId = Guid.NewGuid();
            //_clients.TryAdd(clientId, webSocketContext.WebSocket);
            //_ = HandleClientAsync(clientId, webSocketContext.WebSocket);
          }
          else
          {
            context.Response.StatusCode = 400;
            context.Response.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error accepting client: {ex.Message}");
        }
      }
    }


    protected async Task StopAsync()
    {
      _cts?.Cancel();
      /*foreach (var client in _clients.Values)
      {
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server is shutting down", CancellationToken.None);
      }*/
      _listener?.Stop();

      this.IsRunning = false;
    }
  }



  public class WebSocketClient
  {
    public static async Task<ITransport> ConnectTo(Uri uri, CancellationToken ct)
    {
      ClientWebSocket ws = new ClientWebSocket();
      await ws.ConnectAsync(uri, ct);

      return new WebSocketTransport(ws);
    }
  }

}
