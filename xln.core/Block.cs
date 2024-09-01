using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xln.core
{
  public class Block
  {
    public bool IsLeft { get; set; }
    public string PreviousBlockHash { get; set; } // hash of previous block
    public string PreviousStateHash { get; set; }
    public List<Transition> Transitions { get; set; }
    public int BlockId { get; set; }
    public long Timestamp { get; set; }

    // Конструктор по умолчанию
    public Block()
    {
      Transitions = new List<Transition>();
      PreviousBlockHash = "";
      PreviousStateHash = "";
    }

    // Опциональный конструктор с параметрами
    public Block(bool isLeft, string previousBlockHash, string previousStateHash,
                 List<Transition> transitions, int blockId, long timestamp)
    {
      IsLeft = isLeft;
      PreviousBlockHash = previousBlockHash;
      PreviousStateHash = previousStateHash;
      Transitions = transitions ?? new List<Transition>();
      BlockId = blockId;
      Timestamp = timestamp;
    }

    public Block(Block other)
    {
      IsLeft = other.IsLeft;
      PreviousBlockHash = other.PreviousBlockHash;
      PreviousStateHash = other.PreviousStateHash;
      Transitions = other.Transitions.Select(t => new Transition(t)).ToList();
      BlockId = other.BlockId;
      Timestamp = other.Timestamp;
    }

    public Block DeepClone()
    {
      return new Block(this);
    }
  }
}
