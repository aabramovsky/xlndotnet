using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Util;

namespace xln.core
{
  public enum TransitionType
  {
    AddPayment,
    SettlePayment,
    CancelPayment,
    TextMessage,
    AddSwap,
    SettleSwap,
    AddSubchannel,
    AddDelta,
    DirectPayment,
    SetCreditLimit,
    ProposedEvent
  }

  public abstract class Transition
  {
    public abstract TransitionType Type { get; }
    public abstract void ApplyTo(Channel channel, Block block, bool isDryRun);
    public abstract Transition DeepClone();
  }

  public class AddPayment : Transition
  {
    public override TransitionType Type => TransitionType.AddPayment;
    public int ChainId { get; set; }
    public int TokenId { get; set; }
    public BigInteger Amount { get; set; }
    public string Hashlock { get; set; }
    public long Timelock { get; set; }
    public string EncryptedPackage { get; set; }

    public AddPayment(AddPayment other)
    {
      ChainId = other.ChainId;
      TokenId = other.TokenId;
      Amount = other.Amount;
      Hashlock = other.Hashlock;
      Timelock = other.Timelock;
      EncryptedPackage = other.EncryptedPackage;
    }

    public AddPayment(int chainId, int tokenId, BigInteger amount, string hashlock, long timelock, string encryptedPackage)
    {
      ChainId = chainId;
      TokenId = tokenId;
      Amount = amount;
      Hashlock = hashlock;
      Timelock = timelock;
      EncryptedPackage = encryptedPackage;
    }

    public override Transition DeepClone() => new AddPayment(this);

    public override void ApplyTo(Channel channel, Block block, bool isDryRun)
    {
      ChannelState state = isDryRun ? channel.DryRunState : channel.State;

      Delta delta = state.GetDelta(ChainId, TokenId);

      DerivedDelta derived = state.DeriveDelta(ChainId, TokenId, block.IsLeft);
      if (derived.OutCapacity < Amount)
      {
        throw new Exception($"Fatal no capacity {derived.OutCapacity} < {Amount}");
      }

      var storedSubcontract = new StoredSubcontract
      {
        OriginalTransition = this,
        Timestamp = block.Timestamp,
        IsLeft = block.IsLeft,
        TransitionId = state.TransitionId,
        BlockId = block.BlockId
      };
      state.Subcontracts.Add(storedSubcontract);

      if (!isDryRun)
      {
        bool isSender = (block.IsLeft == channel.IsLeft);
        channel.Owner.ProcessAddPayment(channel, storedSubcontract, isSender);
      }
    }
  }


  public class SettlePayment : Transition
  {
    public override TransitionType Type => TransitionType.SettlePayment;

    public long TransitionId { get; }
    public string Secret { get; }

    public SettlePayment(long transitionId, string secret)
    {
      TransitionId = transitionId;
      Secret = secret;
    }

    public override Transition DeepClone()
    {
      return new SettlePayment(TransitionId, Secret);
    }

    public override async void ApplyTo(Channel channel, Block block, bool isDryRun)
    {
      string hashlock = Keccak256.CalculateHash(Secret);

      ChannelState state = isDryRun ? channel.DryRunState : channel.State;

      if (!isDryRun) Console.WriteLine($"Applying secret, lock {Secret}{hashlock}");

      //TODO WTF?!
      StoredSubcontract subcontract = state.Subcontracts.FirstOrDefault(s =>
          s.TransitionId == TransitionId && s.IsLeft != block.IsLeft);

      if (subcontract == null || !(subcontract.OriginalTransition is AddPayment payment))
      {
        Console.WriteLine($"{channel.PeerAddress} {state} {this} {block}");
        Console.WriteLine($"Fatal no payment {isDryRun} {state}");
        throw new Exception("No such payment");
      }

      if (payment.Hashlock != hashlock)
        throw new Exception("SettlePayment invalid hashlock");

      Delta delta = state.GetDelta(payment.ChainId, payment.TokenId);

      delta.OffDelta += !block.IsLeft ? -payment.Amount : payment.Amount;

      state.Subcontracts.Remove(subcontract);

      if (block.IsLeft != channel.IsLeft && !isDryRun)
      {
        Console.WriteLine($"Payment subcontract settled {Secret}");
      }

      if (!isDryRun)
      {
        bool isSender = (block.IsLeft == channel.IsLeft);
        await channel.Owner.ProcessSettlePayment(channel, subcontract, Secret, isSender);
      }
    }
  }

}
