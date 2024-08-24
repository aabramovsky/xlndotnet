using MessagePack;
using Nethereum.Contracts.QueryHandlers.MultiCall;
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
  public interface ITransport : IDisposable
  {
    Task SendAsync(Message msg, CancellationToken ct);
    Task<Message> ReceiveAsync(CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
  }

  public class WebSocketTransport : ITransport
  {
    protected WebSocket _ws;
    private bool disposed = false;

    public WebSocketTransport(WebSocket ws)
    {
      _ws = ws;
    }

    public async Task SendAsync(Message msg, CancellationToken ct)
    {
      byte[] encodedMsg = MessagePackSerializer.Serialize(msg);
      await _ws.SendAsync(new ArraySegment<byte>(encodedMsg), WebSocketMessageType.Binary, true, ct);
    }

    public async Task<Message> ReceiveAsync(CancellationToken ct)
    {
      const int BufferSize = 4096; // Размер буфера для чтения
      var buffer = new byte[BufferSize];
      var receivedBytes = new List<byte>();

      while (_ws.State == WebSocketState.Open)
      {
        WebSocketReceiveResult result;
        do
        {
          result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
          receivedBytes.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
        }
        while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Binary)
        {
          var messageBytes = receivedBytes.ToArray();
          return DecodeMessage(messageBytes);
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
          await CloseAsync(ct);
          return null;
        }
        else
        {
          throw new InvalidOperationException($"Unexpected WebSocket message type: {result.MessageType}");
        }
      }

      // Если соединение закрыто, возвращаем null или выбрасываем исключение
      return null; // или throw new WebSocketClosedException();
    }

    private Message DecodeMessage(byte[] messageBytes)
    {
      try
      {
        return MessagePackSerializer.Deserialize<Message>(messageBytes);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("Failed to deserialize the received message", ex);
      }
    }


    public async Task CloseAsync(CancellationToken ct)
    {
      try
      {
        // TODO dispose _ws
        await _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct);
      }
      catch (Exception ex)
      { }      
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposed)
      {
        if (disposing)
        {
          // Освобождение управляемых ресурсов
          // Например, закрытие WebSocket соединения
          CloseAsync(CancellationToken.None).Wait();
        }

        // Освобождение неуправляемых ресурсов (если есть)

        disposed = true;
      }
    }
    
    ~WebSocketTransport()
    {
      Dispose(false);
    }
  }
}


