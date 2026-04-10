using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ChatCore;

namespace ClientApp;

public partial class MainWindow : Window
{
    private ChatClient? _client;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ConnectButton_OnClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string host = HostTextBox.Text?.Trim() ?? string.Empty;
        string nick = NickTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            AddMessage("Ошибка: введите адрес сервера.");
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
        {
            AddMessage("Ошибка: введите корректный номер порта (1–65535).");
            return;
        }

        if (string.IsNullOrWhiteSpace(nick))
        {
            AddMessage("Ошибка: введите никнейм.");
            return;
        }

        _client = new ChatClient();

        _client.OnConnected            += OnConnected;
        _client.OnDisconnected         += OnDisconnected;
        _client.OnMessageReceived      += OnMessageReceived;
        _client.OnSystemMessage        += OnSystemMessage;
        _client.OnUsersListReceived    += OnUsersListReceived;
        _client.OnPrivateMessageReceived += OnPrivateMessageReceived;
        _client.OnError                += OnError;

        _client.Connect(host, port, nick);
    }

    private void DisconnectButton_OnClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _client?.Disconnect();
    }

    private void SendButton_OnClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SendCurrentMessage();
    }

    private void MessageInputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendCurrentMessage();
        }
    }

    private void SendCurrentMessage()
    {
        string text = MessageInputBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _client?.SendMessage(text);

        // Локальное отображение своего сообщения
        if (_client != null && _client.IsConnected())
        {
            AddMessage($"{_client.Nick}: {text}");
        }

        MessageInputBox.Text = string.Empty;
    }

    private void OnConnected()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusText.Text = $"🟢 В сети: {_client?.Nick}";
            StatusText.Foreground = Avalonia.Media.Brushes.MediumSpringGreen; // красивый зеленый цвет
            
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;
            SendButton.IsEnabled = true;
            HostTextBox.IsEnabled = false;
            PortTextBox.IsEnabled = false;
            NickTextBox.IsEnabled = false;

            AddMessage("Вы подключились к чату.");
            _client?.RequestUserList();
        });
    }

    private void OnDisconnected(string reason)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusText.Text = "Не подключён";
            StatusText.Foreground = Avalonia.Media.Brushes.LightGray; // возвращаем цвет
            
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            SendButton.IsEnabled = false;
            HostTextBox.IsEnabled = true;
            PortTextBox.IsEnabled = true;
            NickTextBox.IsEnabled = true;

            UsersListBox.Items.Clear();
            AddMessage($"[Система] {reason}");
        });
    }

    private void OnMessageReceived(string nick, string text)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddMessage($"{nick}: {text}");
        });
    }

    private void OnSystemMessage(string text)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddMessage($"[Система] {text}");
            _client?.RequestUserList();
        });
    }

    private void OnUsersListReceived(string[] users)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UsersListBox.Items.Clear();

            foreach (string user in users)
            {
                UsersListBox.Items.Add(user);
            }
        });
    }

    private void OnPrivateMessageReceived(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddMessage(message);
        });
    }

    private void OnError(string error)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddMessage($"[Ошибка] {error}");
        });
    }

    private void AddMessage(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        MessagesListBox.Items.Add(entry);
        MessagesScrollViewer.ScrollToEnd();
    }
}