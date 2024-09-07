using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xln.core
{
  public class StoredSubcontract
  {
    public Transition OriginalTransition { get; set; }
    public bool IsLeft { get; set; }
    public long TransitionId { get; set; }
    public long BlockId { get; set; }
    public long Timestamp { get; set; }
    public object Data { get; set; }

    public StoredSubcontract()
    {
    }

    // Copy constructor
    public StoredSubcontract(StoredSubcontract other)
    {
      OriginalTransition = other.OriginalTransition?.DeepClone();
      IsLeft = other.IsLeft;
      TransitionId = other.TransitionId;
      BlockId = other.BlockId;
      Timestamp = other.Timestamp;
      //Data = DeepCloneData(other.Data);
    }

    public StoredSubcontract DeepClone()
    {
      return new StoredSubcontract(this);
    }

    //private object DeepCloneData(object data)
    //{
    //  if (data == null)
    //    return null;

    //  // Serialize and deserialize to perform a deep clone
    //  string serialized = JsonSerializer.Serialize(data);
    //  return JsonSerializer.Deserialize<object>(serialized);
    //}
  }
}
