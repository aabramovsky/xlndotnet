using Nethereum.JsonRpc.Client;
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
  public class TransportConstants
  {
    public const string AuthorizationHeaderKey = "Authorization";
  }

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
          if (!context.Request.IsWebSocketRequest)
          {
            context.Response.StatusCode = 400;
            context.Response.Close();
          }
          else if (!context.Request.Headers.AllKeys.Contains(TransportConstants.AuthorizationHeaderKey))
          {
            //Console.WriteLine("Authorization header is missing");
            context.Response.StatusCode = 401; // Unauthorized
            context.Response.Close();
          }
          else
          {
            string authHeader = context.Request.Headers[TransportConstants.AuthorizationHeaderKey];
            var webSocketContext = await context.AcceptWebSocketAsync(null);

            IPAddress clientIpAddress = ((IPEndPoint)context.Request.RemoteEndPoint).Address;

            RaiseOnClientConnected(authHeader, clientIpAddress, new WebSocketTransport(webSocketContext.WebSocket));
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
    public static async Task<ITransport> ConnectTo(Uri uri, UserID myId, CancellationToken ct)
    {
      ClientWebSocket ws = new ClientWebSocket();
      ws.Options.SetRequestHeader(TransportConstants.AuthorizationHeaderKey, myId);
      await ws.ConnectAsync(uri, ct);

      return new WebSocketTransport(ws);
    }
  }

}
