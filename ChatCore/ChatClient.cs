using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ChatCore;

public class ChatClient
{
    private TcpClient _tcpClient;
    private StreamReader _reader;
    private StreamWriter _writer;
    private Thread _readThread;
    private bool _isConnected = false;

    public string Nick { get; private set; } = string.Empty;

    // События для подписки из UI
    public event Action<string, string> OnMessageReceived;
    public event Action<string> OnSystemMessage;
    public event Action<string[]> OnUsersListReceived;
    public event Action<string> OnPrivateMessageReceived;
    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<string> OnError;

    public void Connect(string host, int port, string nick)
    {
        if (_isConnected)
        {
            return;
        }

        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(host, port);

            NetworkStream stream = _tcpClient.GetStream();
            _reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            _writer = new StreamWriter(stream, System.Text.Encoding.UTF8)
            {
                AutoFlush = true
            };

            // Отправляем команду входа
            _writer.WriteLine($"{Protocol.CmdJoin} {nick}");

            // Читаем ответ сервера — должен быть WELCOME или ERR
            string response = _reader.ReadLine();

            if (response == null)
            {
                OnError?.Invoke("Сервер закрыл соединение.");
                return;
            }

            if (response.StartsWith(Protocol.PrefixErr))
            {
                OnError?.Invoke(response);
                _tcpClient.Close();
                return;
            }

            if (!response.StartsWith(Protocol.PrefixWelcome))
            {
                OnError?.Invoke($"Неожиданный ответ сервера: {response}");
                _tcpClient.Close();
                return;
            }

            Nick = nick;
            _isConnected = true;

            OnConnected?.Invoke();

            // Запускаем фоновый поток чтения
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "ClientReadThread"
            };
            _readThread.Start();
        }
        catch (SocketException ex)
        {
            OnError?.Invoke($"Не удалось подключиться: {ex.Message}");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Ошибка подключения: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        if (!_isConnected)
        {
            return;
        }

        _isConnected = false;

        try
        {
            _tcpClient.Close();
        }
        catch (Exception)
        {
            // Игнорируем — соединение уже могло быть закрыто
        }

        OnDisconnected?.Invoke("Отключено от сервера.");
    }

    public void SendMessage(string text)
    {
        if (!_isConnected || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            _writer.WriteLine(text);
        }
        catch (IOException)
        {
            HandleConnectionLost();
        }
    }

    public void RequestUserList()
    {
        SendMessage(Protocol.CmdUsers);
    }

    public void SendPrivateMessage(string targetNick, string text)
    {
        SendMessage($"{Protocol.CmdPm} {targetNick} {text}");
    }

    private void ReadLoop()
    {
        try
        {
            while (_isConnected)
            {
                string line = _reader.ReadLine();

                if (line == null)
                {
                    break;
                }

                ParseIncomingLine(line);
            }
        }
        catch (IOException)
        {
            // Соединение разорвано
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Ошибка чтения: {ex.Message}");
        }
        finally
        {
            HandleConnectionLost();
        }
    }

    private void ParseIncomingLine(string line)
    {
        if (line.StartsWith(Protocol.PrefixSys))
        {
            string text = line[Protocol.PrefixSys.Length..].Trim();
            OnSystemMessage?.Invoke(text);
            return;
        }

        if (line.StartsWith(Protocol.PrefixUsers))
        {
            string raw = line[Protocol.PrefixUsers.Length..].Trim();
            string[] users = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < users.Length; i++)
            {
                users[i] = users[i].Trim();
            }

            OnUsersListReceived?.Invoke(users);
            return;
        }

        if (line.StartsWith(Protocol.PrefixPm))
        {
            OnPrivateMessageReceived?.Invoke(line);
            return;
        }

        if (line.StartsWith(Protocol.PrefixErr))
        {
            OnError?.Invoke(line);
            return;
        }

        // Обычное сообщение формата "Ник:Текст"
        int colonIdx = line.IndexOf(':');
        if (colonIdx > 0)
        {
            string nick = line[..colonIdx];
            string text = line[(colonIdx + 1)..];
            OnMessageReceived?.Invoke(nick, text);
            return;
        }

        // Неизвестный формат — показываем как системное
        OnSystemMessage?.Invoke(line);
    }

    private void HandleConnectionLost()
    {
        if (!_isConnected)
        {
            return;
        }

        _isConnected = false;

        try
        {
            _tcpClient.Close();
        }
        catch (Exception)
        {
            // Игнорируем
        }

        OnDisconnected?.Invoke("Соединение с сервером потеряно.");
    }

    public bool IsConnected()
    {
        return _isConnected;
    }
}