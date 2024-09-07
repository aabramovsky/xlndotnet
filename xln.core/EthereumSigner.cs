using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Signer;

namespace xln.core
{
  public class EthereumSigner
  {
    private readonly EthECKey _privateKey;

    public EthereumSigner(string privateKey)
    {
      _privateKey = new EthECKey(privateKey);
    }

    public string SignMessage(string message)
    {
      var signer = new EthereumMessageSigner();
      var encodedMessage = Encoding.UTF8.GetBytes(message);
      return signer.Sign(encodedMessage, _privateKey);
    }

    public string GetAddress()
    {
      return _privateKey.GetPublicAddress();
    }
  }

  // Пример использования:
  // var signer = new EthereumSigner("0x123456..."); // Ваш приватный ключ
  // string signature = signer.SignMessage("Hello, World!");
  // Console.WriteLine($"Signature: {signature}");
  // Console.WriteLine($"Address: {signer.GetAddress()}");
}
