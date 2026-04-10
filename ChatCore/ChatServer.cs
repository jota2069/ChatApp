using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChatCore;

public class ChatServer
{
    private TcpListener _listener;
    private Thread _acceptThread;
    private readonly List<ClientHandler> _clients = new List<ClientHandler>();
    private readonly object _lockObj = new object();
    private bool _isRunning = false;

    public int Port { get; private set; }

    // События для подписки из UI
    public event Action<string> OnClientConnected;
    public event Action<string> OnClientDisconnected;
    public event Action<string, string> OnMessageReceived;
    public event Action<string> OnLogMessage;

    public ChatServer(int port = 5000)
    {
        Port = port;
    }

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        _isRunning = true;

        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "AcceptThread"
        };
        _acceptThread.Start();

        Log($"Сервер запущен на порту {Port}.");
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
            // Игнорируем — слушатель уже мог быть остановлен
        }

        lock (_lockObj)
        {
            foreach (ClientHandler client in _clients)
            {
                client.Disconnect();
            }

            _clients.Clear();
        }

        Log("Сервер остановлен.");
    }

    private void AcceptLoop()
    {
        while (_isRunning)
        {
            try
            {
                TcpClient tcpClient = _listener.AcceptTcpClient();

                ClientHandler handler = new ClientHandler(tcpClient, this);

                Thread clientThread = new Thread(handler.Start)
                {
                    IsBackground = true,
                    Name = $"ClientThread-{tcpClient.Client.RemoteEndPoint}"
                };
                clientThread.Start();
            }
            catch (SocketException)
            {
                // Возникает при вызове _listener.Stop() — выходим из цикла
                break;
            }
            catch (Exception ex)
            {
                Log($"Ошибка при приёме соединения: {ex.Message}");
            }
        }
    }

    // Вызывается из ClientHandler когда клиент успешно прошёл /join
    public void OnClientJoined(ClientHandler handler)
    {
        lock (_lockObj)
        {
            _clients.Add(handler);
        }

        Log($"{handler.Nick} подключился.");
        BroadcastAll(Protocol.SysJoined(handler.Nick));
        OnClientConnected?.Invoke(handler.Nick);
    }

    // Вызывается из ClientHandler при отключении
    public void OnClientLeft(ClientHandler handler)
    {
        bool wasInList = false;

        lock (_lockObj)
        {
            wasInList = _clients.Remove(handler);
        }

        if (!wasInList)
        {
            return;
        }

        Log($"{handler.Nick} отключился.");
        BroadcastAll(Protocol.SysLeft(handler.Nick));
        OnClientDisconnected?.Invoke(handler.Nick);
    }

    // Рассылка всем, кроме отправителя
    public void Broadcast(string message, ClientHandler excludeHandler)
    {
        lock (_lockObj)
        {
            foreach (ClientHandler client in _clients)
            {
                if (client != excludeHandler)
                {
                    client.Send(message);
                }
            }
        }

        // Парсим ник и текст для события UI
        int colonIdx = message.IndexOf(':');
        if (colonIdx > 0)
        {
            string nick = message[..colonIdx];
            string text = message[(colonIdx + 1)..];
            OnMessageReceived?.Invoke(nick, text);
        }
    }

    // Рассылка всем без исключений (системные сообщения)
    private void BroadcastAll(string message)
    {
        lock (_lockObj)
        {
            foreach (ClientHandler client in _clients)
            {
                client.Send(message);
            }
        }
    }

    public void SendPrivate(ClientHandler from, string targetNick, string text)
    {
        ClientHandler target = null;

        lock (_lockObj)
        {
            target = _clients.FirstOrDefault(c => c.Nick == targetNick);
        }

        if (target == null)
        {
            from.Send(Protocol.Error($"Пользователь '{targetNick}' не найден."));
            return;
        }

        string message = Protocol.PrivateMessage(from.Nick, text);
        target.Send(message);
        from.Send(message);

        OnMessageReceived?.Invoke(from.Nick, $"[PM → {targetNick}] {text}");
    }

    public bool IsNickTaken(string nick)
    {
        lock (_lockObj)
        {
            return _clients.Any(c => c.Nick.Equals(nick, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string GetUserList()
    {
        lock (_lockObj)
        {
            return string.Join(", ", _clients.Select(c => c.Nick));
        }
    }

    public int GetClientCount()
    {
        lock (_lockObj)
        {
            return _clients.Count;
        }
    }

    private void Log(string message)
    {
        string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Console.WriteLine(logEntry);
        OnLogMessage?.Invoke(logEntry);
    }
}