using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace xln.core
{
  public class ServerEventArgs : EventArgs
  {
    public ITransport Transport { get; }

    public ServerEventArgs(ITransport transport)
    {
      Transport = transport;
    }
  }

  public abstract class Server
  {
    //private List<ServerEventHandler> _eventHandlers = new List<ServerEventHandler>();

    public bool IsRunning { get; protected set; }
    public string ListenerAddress { get; protected set; }

    public abstract void Start(string uriToListen);

    public abstract void Stop();

    public virtual void Restart()
    {
      Stop();
      Start(ListenerAddress);
    }

    public event EventHandler<ServerEventArgs>? OnClientConnected;

    protected virtual void RaiseOnClientConnected(ITransport transport)
    {
      OnClientConnected?.Invoke(this, new ServerEventArgs(transport));
    }
  }
}
