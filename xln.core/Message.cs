using System;
using MessagePack;
using MessagePack.Resolvers;
using Newtonsoft.Json;

namespace xln.core
{
  [MessagePackObject]
  public class Message
  {
    [Key("header")]
    public Header Header { get; set; }

    [Key("body")]
    public Body Body { get; set; }
  }

  [MessagePackObject]
  public class Header
  {
    [Key("from")]
    public XlnAddress From { get; set; }

    [Key("to")]
    public XlnAddress To { get; set; }
  }

  public enum BodyTypes
  {
    kUndef = 0,
    kFlushMessage = 1,
    kBroadcastProfile = 2,
    kGetProfile = 3,
  }

  [MessagePackObject]
  public class Body
  {
    [Key("type")]
    public BodyTypes Type { get; set; }

    [Key("additionalProperties")]
    public Dictionary<string, object> AdditionalProperties { get; set; }

    public Body(BodyTypes type)
    {
      Type = type;
      AdditionalProperties = new Dictionary<string, object>();
    }

    public void SetProperty(string key, object value)
    {
      AdditionalProperties[key] = value;
    }

    public T GetProperty<T>(string key)
    {
      if (AdditionalProperties.TryGetValue(key, out object value))
      {
        if (value is T typedValue)
        {
          return typedValue;
        }

        // Используем JSON.NET для преобразования типов
        //return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
      }
      //return default(T);
      throw new InvalidOperationException();
    }

    public object GetProperty(string key)
    {
      if (AdditionalProperties.TryGetValue(key, out object value))
        return value;

      throw new InvalidOperationException();
    }
  }


  [MessagePackObject]
  public class FlushMessageBody : Body
  {
    [Key("blockId")]
    public long BlockId { get; set; }

    [Key("pendingSignatures")]
    public List<string> PendingSignatures { get; set; }

    [Key("newSignatures")]
    public List<string>? NewSignatures { get; set; }

    [Key("block")]
    public Block? Block { get; set; }

    [Key("debugState")]
    public string? DebugState { get; set; }

    [Key("counter")]
    public long Counter { get; set; }

    public FlushMessageBody(
        long blockId,
        List<string>? pendingSignatures = null,
        List<string>? newSignatures = null,
        Block? block = null,
        string? debugState = null,
        long counter = 0
    ) : base(BodyTypes.kFlushMessage)
    {
      BlockId = blockId;
      PendingSignatures = pendingSignatures ?? new List<string>();
      NewSignatures = newSignatures;
      Block = block;
      DebugState = debugState;
      Counter = counter;
    }

    // Конструктор без параметров для MessagePack
    [SerializationConstructor]
    public FlushMessageBody() : base(BodyTypes.kFlushMessage)
    {
      PendingSignatures = new List<string>();
    }
  }


  public static class MessageSerializer
  {
    public static byte[] Encode(object obj)
    {
      var options = MessagePackSerializerOptions.Standard.WithResolver(
          CompositeResolver.Create(
              NativeGuidResolver.Instance,
              NativeDateTimeResolver.Instance,
              ContractlessStandardResolver.Instance
          )
      );

      return MessagePackSerializer.Serialize(obj, options);
    }

    public static T Decode<T>(byte[] data)
    {
      var options = MessagePackSerializerOptions.Standard.WithResolver(
          CompositeResolver.Create(
              NativeGuidResolver.Instance,
              NativeDateTimeResolver.Instance,
              ContractlessStandardResolver.Instance
          )
      );

      return MessagePackSerializer.Deserialize<T>(data, options);
    }

    public static T DecodeFromString<T>(string base64EncodedData)
    {
      byte[] data = Convert.FromBase64String(base64EncodedData);
      return Decode<T> (data);
    }
  }
}
