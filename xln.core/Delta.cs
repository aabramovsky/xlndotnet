using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace xln.core
{
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

    public Delta DeepClone()
    {
      return new Delta
      {
        TokenId = TokenId,
        Collateral = Collateral,
        OnDelta = OnDelta,
        OffDelta = OffDelta,
        LeftCreditLimit = LeftCreditLimit,
        RightCreditLimit = RightCreditLimit,
        LeftAllowance = LeftAllowance,
        RightAllowance = RightAllowance
      };
    }

    public object Clone()
    {
      return DeepClone();
    }
  }
  public class DerivedDelta
  {
    public BigInteger Delta { get; set; }
    public BigInteger Collateral { get; set; }
    public BigInteger InCollateral { get; set; }
    public BigInteger OutCollateral { get; set; }
    public BigInteger InOwnCredit { get; set; }
    public BigInteger OutPeerCredit { get; set; }
    public BigInteger InAllowance { get; set; }
    public BigInteger OutAllowance { get; set; }
    public BigInteger TotalCapacity { get; set; }
    public BigInteger OwnCreditLimit { get; set; }
    public BigInteger PeerCreditLimit { get; set; }
    public BigInteger InCapacity { get; set; }
    public BigInteger OutCapacity { get; set; }
    public BigInteger OutOwnCredit { get; set; }
    public BigInteger InPeerCredit { get; set; }
  }
}
