using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xln.core
{
  public class UserID
  {
    private readonly string _value;

    public UserID(string value)
    {
      _value = value;
    }

    public static implicit operator string(UserID userId) => userId._value;
    public static implicit operator UserID(string value) => new UserID(value);

    public override string ToString() => _value;
  }
}
