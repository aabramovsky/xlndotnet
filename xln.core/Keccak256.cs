using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xln.core
{
  public class Keccak256
  {
    public static string AsString(byte[] hashBytes)
    {
      string hashString = "0x" + BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); ;
      return hashString;
    }

    public static byte[] CalculateHash(byte[] packedData)
    {
      return Sha3Keccack.Current.CalculateHash(packedData);
    }

    public static string CalculateHashString(byte[] packedData)
    {
      return AsString(CalculateHash(packedData));
    }
  }
}
