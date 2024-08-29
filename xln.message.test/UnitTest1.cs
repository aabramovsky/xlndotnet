using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using xln.core;


namespace xln.message.test
{
  [TestFixture]
  public class MessageTests
  {
    [Test]
    public void Message_Properties_SetAndGet()
    {
      var message = new Message
      {
        Header = new Header { From = "sender", To = "receiver" },
        Body = new Body(BodyTypes.kBroadcastProfile)
      };

      Assert.AreEqual(new XlnAddress("sender"), message.Header.From);
      Assert.AreEqual(new XlnAddress("receiver"), message.Header.To);
      Assert.AreEqual(BodyTypes.kBroadcastProfile, message.Body.Type);
    }

    [Test]
    public void Body_AdditionalProperties_SetAndGet()
    {
      var body = new Body(BodyTypes.kGetProfile);
      body.SetProperty("key1", "value1");
      body.SetProperty("key2", 42);

      Assert.AreEqual("value1", body.GetProperty<string>("key1"));
      Assert.AreEqual(42, body.GetProperty<int>("key2"));
    }

    [Test]
    public void Body_GetProperty_ThrowsException_WhenKeyNotFound()
    {
      var body = new Body(BodyTypes.kFlushMessage);

      Assert.Throws<InvalidOperationException>(() => body.GetProperty("nonexistent"));
    }

    [Test]
    public void MessageSerializer_EncodeAndDecode()
    {
      var originalMessage = new Message
      {
        Header = new Header { From = "sender", To = "receiver" },
        Body = new Body(BodyTypes.kBroadcastProfile)
      };
      originalMessage.Body.SetProperty("testKey", "testValue");

      byte[] encoded = MessageSerializer.Encode(originalMessage);
      Message decodedMessage = MessageSerializer.Decode(encoded);

      Assert.AreEqual(originalMessage.Header.From, decodedMessage.Header.From);
      Assert.AreEqual(originalMessage.Header.To, decodedMessage.Header.To);
      Assert.AreEqual(originalMessage.Body.Type, decodedMessage.Body.Type);
      Assert.AreEqual("testValue", decodedMessage.Body.GetProperty<string>("testKey"));
    }

    [Test]
    public void Body_GetProperty_Generic_ThrowsException_WhenTypesMismatch()
    {
      var body = new Body(BodyTypes.kFlushMessage);
      body.SetProperty("key", "string value");

      Assert.Throws<InvalidOperationException>(() => body.GetProperty<int>("key"));
    }

    [Test]
    public void MessageSerializer_Encode_HandlesComplexTypes()
    {
      var message = new Message
      {
        Header = new Header { From = "sender", To = "receiver" },
        Body = new Body(BodyTypes.kBroadcastProfile)
      };
      message.Body.SetProperty("date", DateTime.Now);
      message.Body.SetProperty("guid", Guid.NewGuid());
      message.Body.SetProperty("list", new List<int> { 1, 2, 3 });

      byte[] encoded = MessageSerializer.Encode(message);
      Assert.NotNull(encoded);
      Assert.Greater(encoded.Length, 0);
    }
  }
}