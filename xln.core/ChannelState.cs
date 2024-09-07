using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace xln.core
{
  public class ChannelState
  {
    public XlnAddress Left { get; set; }
    public XlnAddress Right { get; set; }
    public string ChannelKey { get; set; }
    public string PreviousBlockHash { get; set; }
    public string PreviousStateHash { get; set; }
    public long BlockId { get; set; }
    public long Timestamp { get; set; }
    public long TransitionId { get; set; }
    public List<Subchannel> Subchannels { get; set; } = new List<Subchannel>();

    public List<StoredSubcontract> Subcontracts { get; set; } = new List<StoredSubcontract>();

    public ChannelState()
    {
      Left = new XlnAddress();  // Предполагается, что у XlnAddress есть конструктор по умолчанию
      Right = new XlnAddress();
      ChannelKey = "0x0";
      PreviousBlockHash = "0x0";
      PreviousStateHash = "0x0";
      BlockId = 0;
      Timestamp = 0;
      TransitionId = 0;
      Subchannels = new List<Subchannel>();
      Subcontracts = new List<StoredSubcontract>();
    }

    public ChannelState DeepClone()
    {
      return new ChannelState
      {
        Left = new XlnAddress(Left.ToString()), // Create a new XlnAddress with the same value
        Right = new XlnAddress(Right.ToString()),
        ChannelKey = ChannelKey,
        PreviousBlockHash = PreviousBlockHash,
        PreviousStateHash = PreviousStateHash,
        BlockId = BlockId,
        Timestamp = Timestamp,
        TransitionId = TransitionId,
        Subchannels = Subchannels?.Select(s => s.DeepClone()).ToList(),
        Subcontracts = Subcontracts?.Select(s => s.DeepClone()).ToList()
      };
    }

    // Implement ICloneable interface
    public object Clone()
    {
      return DeepClone();
    }

    public Delta GetDelta(int chainId, int tokenId)
    {
      Subchannel subchannel = GetSubchannel(chainId);
      return subchannel.FindDeltaByTokenId(tokenId);
    }

    public Subchannel GetSubchannel(int chainId)
    {
      foreach (Subchannel subchannel in Subchannels)
      {
        if (subchannel.ChainId == chainId)
          return subchannel;
      }
      throw new InvalidOperationException($"Subchannel with chainId {chainId} not found.");
    }

    public DerivedDelta DeriveDelta(int chainId, int tokenId, bool isLeft)
    {
      Delta d = GetDelta(chainId, tokenId);

      BigInteger NonNegative(BigInteger x) => x < BigInteger.Zero ? BigInteger.Zero : x;

      BigInteger delta = d.OnDelta + d.OffDelta;
      BigInteger collateral = NonNegative(d.Collateral);

      BigInteger ownCreditLimit = d.LeftCreditLimit;
      BigInteger peerCreditLimit = d.RightCreditLimit;

      BigInteger inCollateral = delta > BigInteger.Zero ? NonNegative(collateral - delta) : collateral;
      BigInteger outCollateral = delta > BigInteger.Zero ? (delta > collateral ? collateral : delta) : BigInteger.Zero;

      BigInteger inOwnCredit = NonNegative(-delta);
      if (inOwnCredit > ownCreditLimit) inOwnCredit = ownCreditLimit;

      BigInteger outPeerCredit = NonNegative(delta - collateral);
      if (outPeerCredit > peerCreditLimit) outPeerCredit = peerCreditLimit;

      BigInteger outOwnCredit = NonNegative(ownCreditLimit - inOwnCredit);
      BigInteger inPeerCredit = NonNegative(peerCreditLimit - outPeerCredit);

      BigInteger inAllowance = d.RightAllowance;
      BigInteger outAllowance = d.LeftAllowance;

      BigInteger totalCapacity = collateral + ownCreditLimit + peerCreditLimit;

      BigInteger inCapacity = NonNegative(inOwnCredit + inCollateral + inPeerCredit - inAllowance);
      BigInteger outCapacity = NonNegative(outPeerCredit + outCollateral + outOwnCredit - outAllowance);

      if (!isLeft)
      {
        // Flip the view
        (inCollateral, inAllowance, inCapacity,
         outCollateral, outAllowance, outCapacity) =
        (outCollateral, outAllowance, outCapacity,
         inCollateral, inAllowance, inCapacity);

        (ownCreditLimit, peerCreditLimit) = (peerCreditLimit, ownCreditLimit);

        // Swap in<->out own<->peer credit
        (outOwnCredit, inOwnCredit, outPeerCredit, inPeerCredit) =
        (inPeerCredit, outPeerCredit, inOwnCredit, outOwnCredit);
      }

      return new DerivedDelta
      {
        Delta = delta,
        Collateral = collateral,
        InCollateral = inCollateral,
        OutCollateral = outCollateral,
        InOwnCredit = inOwnCredit,
        OutPeerCredit = outPeerCredit,
        InAllowance = inAllowance,
        OutAllowance = outAllowance,
        TotalCapacity = totalCapacity,
        OwnCreditLimit = ownCreditLimit,
        PeerCreditLimit = peerCreditLimit,
        InCapacity = inCapacity,
        OutCapacity = outCapacity,
        OutOwnCredit = outOwnCredit,
        InPeerCredit = inPeerCredit
      };
    }
  }

  public class Subchannel
  {
    public int ChainId { get; set; }
    public List<Delta> Deltas { get; set; } = new List<Delta>();
    public long CooperativeNonce { get; set; }
    public long DisputeNonce { get; set; }

    //public List<ProposedEvent> ProposedEvents { get; set; } = new List<ProposedEvent>();
    public bool ProposedEventsByLeft { get; set; }

    public Subchannel DeepClone()
    {
      return new Subchannel
      {
        ChainId = ChainId,
        Deltas = Deltas.Select(d => d.DeepClone()).ToList(),
        CooperativeNonce = CooperativeNonce,
        DisputeNonce = DisputeNonce,
        //ProposedEvents = ProposedEvents?.Select(pe => pe.DeepClone()).ToList(), // Uncomment if needed
        ProposedEventsByLeft = ProposedEventsByLeft
      };
    }

    public object Clone()
    {
      return DeepClone();
    }

    //TODO сделать дельты не списком, а каким-то вменяемым типом для поиска
    public Delta FindDeltaByTokenId(int tokenId)
    {
      foreach (Delta delta in Deltas)
      {
        if (delta.TokenId == tokenId)
          return delta;
      }
      throw new InvalidOperationException($"Delta with tokenId {tokenId} not found.");
    }
  }
}
