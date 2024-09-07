using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xln.core
{
  public class XlnAddress
  {
    private readonly string _value;

    public XlnAddress()
    {
      _value = "0x";
    }

    public XlnAddress(string value)
    {
      _value = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value : "0x" + value;
    }

    public static implicit operator string(XlnAddress userId) => userId._value;
    public static implicit operator XlnAddress(string value) => new XlnAddress(value);

    public override string ToString() => _value;

    public static bool operator <(XlnAddress left, XlnAddress right)
    {
      return string.Compare(left._value, right._value, StringComparison.Ordinal) < 0;
    }

    public static bool operator >(XlnAddress left, XlnAddress right)
    {
      return string.Compare(left._value, right._value, StringComparison.Ordinal) > 0;
    }

    public int CompareTo(XlnAddress other)
    {
      if (other == null) return 1;
      return string.Compare(_value, other._value, StringComparison.Ordinal);
    }
  }
}
