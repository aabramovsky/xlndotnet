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
    public string From { get; set; }

    [Key("to")]
    public string To { get; set; }
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

  public static class MessageSerializer
  {
    public static byte[] Encode(Message message)
    {
      var options = MessagePackSerializerOptions.Standard.WithResolver(
          CompositeResolver.Create(
              NativeGuidResolver.Instance,
              NativeDateTimeResolver.Instance,
              ContractlessStandardResolver.Instance
          )
      );

      return MessagePackSerializer.Serialize(message, options);
    }

    public static Message Decode(byte[] data)
    {
      var options = MessagePackSerializerOptions.Standard.WithResolver(
          CompositeResolver.Create(
              NativeGuidResolver.Instance,
              NativeDateTimeResolver.Instance,
              ContractlessStandardResolver.Instance
          )
      );

      return MessagePackSerializer.Deserialize<Message>(data, options);
    }
  }
}
