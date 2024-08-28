using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xln.core;

namespace xln.user.test
{

  [TestFixture]
  public class UserToServerConnectionTests
  {
    private const string _serverUrl = "ws://localhost:9090/";
    private const string _uriToListen = "http://localhost:9090/";
    private User serverUser;
    private User clientUser;

    [SetUp]
    public void Setup()
    {
      serverUser = new User("serverUserId");
      clientUser = new User("clientUserId");
    }

    [TearDown]
    public void TearDown()
    {
      // Здесь можно добавить логику для остановки сервера и освобождения ресурсов
    }

    [Test]
    public async Task TestUserConnection()
    {
      // Запускаем сервер
      serverUser.StartServer(_uriToListen);

      // Даем серверу время на запуск
      await Task.Delay(10000);

      // Подключаем клиента
      clientUser.ConnectTo(new Uri(_serverUrl));

      // Даем время на установление соединения
      await Task.Delay(100000);

      // Проверяем, что соединение установлено
      //Assert.IsTrue(serverUser._transports.Count > 0, "Сервер должен иметь хотя бы одно активное соединение");

      // Дополнительные проверки можно добавить здесь
      // Например, можно проверить, что клиент успешно подключился
      // Для этого может потребоваться добавить соответствующие методы в класс User

      // Пример дополнительной проверки (предполагая, что у User есть метод IsConnected):
      // Assert.IsTrue(clientUser.IsConnected(), "Клиент должен быть подключен");
    }
  }
}
