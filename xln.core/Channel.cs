using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

#if false

namespace xln.core
{
  public class ChannelState
  {
    public string Left { get; set; }
    public string Right { get; set; }
    public string ChannelKey { get; set; }
    public string PreviousBlockHash { get; set; }
    public string PreviousStateHash { get; set; }
    public long BlockId { get; set; }
    public long Timestamp { get; set; }
    public long TransitionId { get; set; }
    public List<Subchannel> Subchannels { get; set; } = new List<Subchannel>();
    public List<StoredSubcontract> Subcontracts { get; set; } = new List<StoredSubcontract>();
  }

  public class Subchannel
  {
    public int ChainId { get; set; }
    public List<Delta> Deltas { get; set; } = new List<Delta>();
    public long CooperativeNonce { get; set; }
    public long DisputeNonce { get; set; }
    public List<ProposedEvent> ProposedEvents { get; set; } = new List<ProposedEvent>();
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


  public class Channel
  {
    private readonly ILogger<Channel> _logger;
    private readonly IChannelStorage _channelStorage;

    public ChannelState State { get; private set; }

    public Channel(string leftAddress, string rightAddress, ILogger<Channel> logger, IChannelStorage channelStorage)
    {
      _logger = logger;
      _channelStorage = channelStorage;
      State = new ChannelState
      {
        Left = leftAddress,
        Right = rightAddress,
        // Initialize other state properties
      };
    }

    public void ApplyTransition(ITransition transition)
    {
      transition.Apply(this);
      SaveState();
    }

    private void SaveState()
    {
      _channelStorage.SaveChannelState(State);
    }

    // Implement other channel-related methods
  }
}

#endif