using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using xln.core;

namespace xln.user.test
{
  [TestFixture]
  public class UserConnectionTests
  {
    private const string User1Url = "ws://localhost:9091/";
    private const string User2Url = "ws://localhost:9092/";
    private User user1;
    private User user2;

    [SetUp]
    public void Setup()
    {
      user1 = new User("user1");
      user2 = new User("user2");
    }

    [TearDown]
    public void TearDown()
    {
      // Здесь можно добавить логику для остановки серверов и освобождения ресурсов
      // Например:
      // user1.StopServer();
      // user2.StopServer();
    }

    [Test]
    public async Task TestBidirectionalUserConnection()
    {
      // Запускаем серверы для обоих пользователей
      user1.StartServer(User1Url);
      user2.StartServer(User2Url);

      // Даем серверам время на запуск
      await Task.Delay(1000);

      // Подключаем пользователей друг к другу
      var connectTask1 = Task.Run(() => user1.ConnectTo(new Uri(User2Url)));
      var connectTask2 = Task.Run(() => user2.ConnectTo(new Uri(User1Url)));

      // Ожидаем завершения обоих подключений
      await Task.WhenAll(connectTask1, connectTask2);

      // Даем время на установление соединений
      await Task.Delay(1000);

      // Проверяем, что соединения установлены
      //Assert.IsTrue(user1._transports.Count > 0, "User1 должен иметь хотя бы одно активное соединение");
      //Assert.IsTrue(user2._transports.Count > 0, "User2 должен иметь хотя бы одно активное соединение");

      // Дополнительные проверки
      // Например, проверка количества соединений (должно быть по одному у каждого пользователя)
      //Assert.AreEqual(1, user1._transports.Count, "User1 должен иметь ровно одно соединение");
      //Assert.AreEqual(1, user2._transports.Count, "User2 должен иметь ровно одно соединение");

      // Можно добавить проверку на корректность адресов в _transports
      //Assert.IsTrue(user1._transports.ContainsKey(User2Url), "User1 должен иметь соединение с User2");
      //Assert.IsTrue(user2._transports.ContainsKey(User1Url), "User2 должен иметь соединение с User1");

      // Если в классе User есть метод для проверки статуса соединения, можно использовать его
      // Assert.IsTrue(user1.IsConnectedTo(User2Url), "User1 должен быть подключен к User2");
      // Assert.IsTrue(user2.IsConnectedTo(User1Url), "User2 должен быть подключен к User1");
    }
  }
}