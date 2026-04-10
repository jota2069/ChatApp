using System;
using System.IO;
using System.Net.Sockets;

namespace ChatCore;

public class ClientHandler
{
    private readonly TcpClient _tcpClient;
    private readonly ChatServer _server;
    private StreamReader _reader;
    private StreamWriter _writer;

    public string Nick { get; private set; } = string.Empty;
    public bool IsConnected { get; private set; } = false;

    public ClientHandler(TcpClient tcpClient, ChatServer server)
    {
        _tcpClient = tcpClient;
        _server = server;
    }

    public void Start()
    {
        NetworkStream stream = _tcpClient.GetStream();
        _reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        _writer = new StreamWriter(stream, System.Text.Encoding.UTF8)
        {
            AutoFlush = true
        };

        try
        {
            // Первая строка обязана быть командой /join
            string firstLine = _reader.ReadLine();

            if (firstLine == null || !Protocol.TryParseJoin(firstLine, out string nick))
            {
                Send(Protocol.Error("Первая команда должна быть /join <никнейм>."));
                Disconnect();
                return;
            }

            if (_server.IsNickTaken(nick))
            {
                Send(Protocol.Error("Никнейм уже занят."));
                Disconnect();
                return;
            }

            Nick = nick;
            IsConnected = true;

            Send(Protocol.Welcome(Nick));
            _server.OnClientJoined(this);

            // Основной цикл чтения сообщений
            while (IsConnected)
            {
                string line = _reader.ReadLine();

                if (line == null)
                {
                    break;
                }

                HandleLine(line);
            }
        }
        catch (IOException)
        {
            // Клиент закрыл соединение без предупреждения — это нормально
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientHandler] Неожиданная ошибка ({Nick}): {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    private void HandleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (line == Protocol.CmdUsers)
        {
            string userList = _server.GetUserList();
            Send(Protocol.UsersList(userList));
            return;
        }

        if (Protocol.TryParsePm(line, out string target, out string text))
        {
            _server.SendPrivate(this, target, text);
            return;
        }

        // Всё остальное — обычное сообщение в общий чат
        _server.Broadcast(Protocol.ChatMessage(Nick, line), excludeHandler: this);
    }

    public void Send(string message)
    {
        try
        {
            _writer?.WriteLine(message);
        }
        catch (IOException)
        {
            IsConnected = false;
        }
    }

    public void Disconnect()
    {
        if (!IsConnected && Nick == string.Empty)
        {
            return;
        }

        IsConnected = false;

        try
        {
            _tcpClient.Close();
        }
        catch (Exception)
        {
            // Игнорируем — соединение уже могло быть закрыто
        }

        if (Nick != string.Empty)
        {
            _server.OnClientLeft(this);
        }
    }
}