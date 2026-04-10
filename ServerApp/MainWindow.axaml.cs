using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ChatCore;

namespace ServerApp;

public partial class MainWindow : Window
{
    private ChatServer? _server;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void StartButton_OnClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
        {
            AddLog("Ошибка: введите корректный номер порта (1–65535).");
            return;
        }

        _server = new ChatServer(port);

        _server.OnLogMessage += OnLogMessage;
        _server.OnClientConnected += OnClientConnected;
        _server.OnClientDisconnected += OnClientDisconnected;

        try
        {
            _server.Start();

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PortTextBox.IsEnabled = false;
            StatusText.Text = $"Запущен на порту {port}";
        }
        catch (Exception ex)
        {
            AddLog($"Не удалось запустить сервер: {ex.Message}");
        }
    }

    private void StopButton_OnClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _server?.Stop();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        PortTextBox.IsEnabled = true;
        StatusText.Text = "Остановлен";
        ClientCountText.Text = "0";
    }

    private void OnLogMessage(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddLog(message);
        });
    }

    private void OnClientConnected(string nick)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ClientCountText.Text = _server?.GetClientCount().ToString() ?? "0";
        });
    }

    private void OnClientDisconnected(string nick)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ClientCountText.Text = _server?.GetClientCount().ToString() ?? "0";
        });
    }

    private void AddLog(string message)
    {
        LogListBox.Items.Add(message);
        LogScrollViewer.ScrollToEnd();
    }
}