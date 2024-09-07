using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace xln.core
{
  public class SubchannelProofs
  {
    public List<string> EncodedProofBody { get; set; }
    public List<SubcontractProviderBatch> SubcontractBatch { get; set; }
    public List<ProofBody> ProofBody { get; set; }
    public List<string> ProofHash { get; set; }
    public List<string> Signatures { get; set; }
  }

  public class ProofBody
  {
    public List<BigInteger> OffDeltas { get; set; }
    public List<int> TokenIds { get; set; }
    public List<SubcontractInfo> Subcontracts { get; set; }
  }

  public class SubcontractInfo
  {
    public string SubcontractProviderAddress { get; set; }
    public string EncodedBatch { get; set; }
    public List<object> Allowances { get; set; }
  }

  public class SubcontractProviderBatch
  {
    public List<PaymentSubcontract> Payment { get; set; }
    public List<SwapSubcontract> Swap { get; set; }
  }

  public class PaymentSubcontract
  {
    public int DeltaIndex { get; set; }
    public BigInteger Amount { get; set; }
    public long RevealedUntilBlock { get; set; }
    public string Hash { get; set; }
  }

  public class SwapSubcontract
  {
    public bool OwnerIsLeft { get; set; }
    public int AddDeltaIndex { get; set; }
    public BigInteger AddAmount { get; set; }
    public int SubDeltaIndex { get; set; }
    public BigInteger SubAmount { get; set; }
  }

  public static class ENV
  {
    public static string SubcontractProviderAddress { get; set; }
  }

  public static class AbiDefinitions
  {
    public static readonly string[] SubcontractBatchABI = new string[]
    {
            "tuple",
            "tuple[]",
            "uint256",
            "int256",
            "uint256",
            "bytes32",
            "tuple[]",
            "bool",
            "uint256",
            "uint256",
            "uint256",
            "uint256"
    };

    public static readonly string[] ProofbodyABI = new string[]
    {
            "tuple",
            "int256[]",
            "uint256[]",
            "tuple[]",
            "address",
            "bytes",
            "tuple[]",
            "uint256",
            "uint256",
            "uint256"
    };
  }

  enum MessageType
  {
    CooperativeUpdate,
    CooperativeDisputeProof,
    DisputeProof
  }
}
