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
    public static byte[] CalculateHash(byte[] packedData)
    {
      return Sha3Keccack.Current.CalculateHash(packedData);
    }

    public static string CalculateHash(string str)
    {
      byte[] secretBytes = StringToByteArray(str);     
      return CalculateHashString(secretBytes);
    }

    public static string CalculateHashString(byte[] packedData)
    {
      return AsString(CalculateHash(packedData));
    }

    public static string AsString(byte[] hashBytes)
    {
      string hashString = "0x" + BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); ;
      return hashString;
    }

    public static byte[] HexToByteArray(string hex)
    {
      if (hex.StartsWith("0x"))
        hex = hex.Substring(2);

      byte[] bytes = new byte[hex.Length / 2];
      for (int i = 0; i < bytes.Length; i++)
      {
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
      }
      return bytes;
    }

    public static byte[] StringToByteArray(string str) 
    {
      return System.Text.Encoding.UTF8.GetBytes(str);
    }
  }
}
