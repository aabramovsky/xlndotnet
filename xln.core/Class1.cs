﻿using Nethereum.Web3;
using Nethereum.Util;
using Nethereum.Signer;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using System.Numerics;


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


  public class XlnEntityAddress
  {
    private readonly string _value;

    public XlnEntityAddress(string value)
    {
      _value = value;
    }

    public static implicit operator string(XlnEntityAddress userId) => userId._value;
    public static implicit operator XlnEntityAddress(string value) => new XlnEntityAddress(value);

    public override string ToString() => _value;
  }


  public class User
  {
    private readonly ILogger<User> _logger;
    private readonly Web3 _web3;
    private readonly IChannelStorage _channelStorage;
    private readonly ITransport _transport;

    public string Username { get; }
    public string Address { get; }

    public User(string username, string privateKey, ILogger<User> logger, Web3 web3, IChannelStorage channelStorage, ITransport transport)
    {
      Username = username;
      _logger = logger;
      _web3 = web3;
      _channelStorage = channelStorage;
      _transport = transport;

      var ecKey = new EthECKey(privateKey);
      Address = ecKey.GetPublicAddress();
    }

    public void Start()
    {
      _channelStorage.Initialize(Address);
      _transport.Open();
    }

    public void SendMessage(string recipientAddress, IMessage message)
    {
      _transport.Send(recipientAddress, message);
    }

    // Implement other methods (CreateChannel, ProcessPayment, etc.)
  }

  public class Channel
  {
    private readonly ILogger<Channel> _logger;
    private readonly IChannelStorage _channelStorage;

    public ChannelState State { get; private set; }

    public Channel(string leftAddress, string rightAddress, ILogger<Channel> logger, IChannelStorage channelStorage)
    {
      _logger = logger;
      _channelStorage = channelStorage;
      State = new ChannelState
      {
        Left = leftAddress,
        Right = rightAddress,
        // Initialize other state properties
      };
    }

    public void ApplyTransition(ITransition transition)
    {
      transition.Apply(this);
      SaveState();
    }

    private void SaveState()
    {
      _channelStorage.SaveChannelState(State);
    }

    // Implement other channel-related methods
  }

  public interface ITransition
  {
    void Apply(Channel channel);
  }

  public class AddPaymentTransition : ITransition
  {
    public int ChainId { get; set; }
    public int TokenId { get; set; }
    public BigInteger Amount { get; set; }
    public string Hashlock { get; set; }
    public long Timelock { get; set; }
    public string EncryptedPackage { get; set; }

    public void Apply(Channel channel)
    {
      // Implement logic to add payment to channel state
      // Update deltas, check capacities, etc.
    }
  }

  public interface IChannelStorage
  {
    void Initialize(string userId);
    void SaveChannelState(ChannelState state);
    ChannelState LoadChannelState(string channelId);
  }

  public class ChannelStorage : IChannelStorage
  {
    private readonly ILogger<ChannelStorage> _logger;

    public ChannelStorage(ILogger<ChannelStorage> logger)
    {
      _logger = logger;
    }

    public void Initialize(string userId)
    {
      // Initialize storage for user
    }

    public void SaveChannelState(ChannelState state)
    {
      // Save channel state to storage
    }

    public ChannelState LoadChannelState(string channelId)
    {
      // Load channel state from storage
      throw new NotImplementedException();
    }
  }

  public interface ITransport
  {
    void Open();
    void Send(string recipientAddress, IMessage message);
    void Close();
  }

  public class WebSocketTransport : ITransport
  {
    private readonly ILogger<WebSocketTransport> _logger;
    private ClientWebSocket _webSocket;

    public WebSocketTransport(ILogger<WebSocketTransport> logger)
    {
      _logger = logger;
      _webSocket = new ClientWebSocket();
    }

    public void Open()
    {
      // Initialize WebSocket connection synchronously
      // Note: This is not recommended in real applications
      _webSocket.ConnectAsync(new Uri("ws://localhost:8080"), CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Send(string recipientAddress, IMessage message)
    {
      var json = JsonConvert.SerializeObject(message);
      var buffer = Encoding.UTF8.GetBytes(json);
      _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Close()
    {
      _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None).GetAwaiter().GetResult();
    }
  }

  public class UserManager
  {
    private readonly ILogger<UserManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, User> _users = new Dictionary<string, User>();

    public UserManager(ILogger<UserManager> logger, IServiceProvider serviceProvider)
    {
      _logger = logger;
      _serviceProvider = serviceProvider;
    }

    public void InitializeUsers()
    {
      var usernames = new[] { "alice", "bob", "charlie" };
      foreach (var username in usernames)
      {
        var user = CreateUser(username);
        user.Start();
        _users[user.Address] = user;
      }
    }

    private User CreateUser(string username)
    {
      var privateKey = GeneratePrivateKey(username);
      return null;//ActivatorUtilities.CreateInstance<User>(_serviceProvider, username, privateKey);
    }

    private string GeneratePrivateKey(string username)
    {
      // Generate a deterministic private key for demo purposes
      return Sha3Keccack.Current.CalculateHash(username + "password");
    }

    public void SetupFullMeshNetwork()
    {
      var userList = new List<User>(_users.Values);
      for (int i = 0; i < userList.Count; i++)
      {
        for (int j = i + 1; j < userList.Count; j++)
        {
          SetupChannel(userList[i], userList[j]);
        }
      }
    }

    private void SetupChannel(User user1, User user2)
    {
      // Implement channel setup logic
      _logger.LogInformation($"Setting up channel between {user1.Username} and {user2.Username}");
    }
  }



  public class ChannelState
  {
    public string Left { get; set; }
    public string Right { get; set; }
    public string ChannelKey { get; set; }
    public string PreviousBlockHash { get; set; }
    public string PreviousStateHash { get; set; }
    public long BlockId { get; set; }
    public long Timestamp { get; set; }
    public long TransitionId { get; set; }
    public List<Subchannel> Subchannels { get; set; } = new List<Subchannel>();
    public List<StoredSubcontract> Subcontracts { get; set; } = new List<StoredSubcontract>();
  }

  public class Subchannel
  {
    public int ChainId { get; set; }
    public List<Delta> Deltas { get; set; } = new List<Delta>();
    public long CooperativeNonce { get; set; }
    public long DisputeNonce { get; set; }
    public List<ProposedEvent> ProposedEvents { get; set; } = new List<ProposedEvent>();
    public bool ProposedEventsByLeft { get; set; }
  }

  public class Delta
  {
    public int TokenId { get; set; }
    public BigInteger Collateral { get; set; }
    public BigInteger OnDelta { get; set; }
    public BigInteger OffDelta { get; set; }
    public BigInteger LeftCreditLimit { get; set; }
    public BigInteger RightCreditLimit { get; set; }
    public BigInteger LeftAllowance { get; set; }
    public BigInteger RightAllowance { get; set; }
  }

  public class StoredSubcontract
  {
    public ITransition OriginalTransition { get; set; }
    public long Timestamp { get; set; }
    public bool IsLeft { get; set; }
    public long TransitionId { get; set; }
    public long BlockId { get; set; }
  }

  public class ProposedEvent
  {
    public int ChainId { get; set; }
    public int TokenId { get; set; }
    public BigInteger Collateral { get; set; }
    public BigInteger OnDelta { get; set; }
  }

  public interface IMessage
  {
    MessageHeader Header { get; set; }
    object Body { get; set; }
  }

  public class MessageHeader
  {
    public required string From { get; set; }
    public required string To { get; set; }
  }

}
