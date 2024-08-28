using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace xln.core
{
  public class ThreadSafeTransportStorage
  {
    private readonly ConcurrentDictionary<XlnAddress, ITransport> _transports;

    public ThreadSafeTransportStorage()
    {
      _transports = new ConcurrentDictionary<XlnAddress, ITransport>();
    }

    public bool TryAdd(XlnAddress xlnAddress, ITransport transport)
    {
      return _transports.TryAdd(xlnAddress, transport);
    }

    public ITransport GetOrThrow(XlnAddress xlnAddress)
    {
      if (_transports.TryGetValue(xlnAddress, out var transport))
      {
        return transport;
      }
      throw new KeyNotFoundException("Transport not found for the given XlnAddress.");
    }

    public bool TryGet(XlnAddress xlnAddress, out ITransport transport)
    {
      return _transports.TryGetValue(xlnAddress, out transport);
    }

    public bool TryRemove(XlnAddress xlnAddress, out ITransport transport)
    {
      return _transports.TryRemove(xlnAddress, out transport);
    }

    public int Count => _transports.Count;

    public IEnumerable<XlnAddress> GetAllAddresses()
    {
      return _transports.Keys;
    }

    public IEnumerable<ITransport> GetAllTransports()
    {
      return _transports.Values;
    }
  }
}
