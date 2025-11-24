using client.Models;
using client.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Windows;

namespace client.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private CancellationTokenSource _cts = new();
    private TcpClient? _client;
    
    [Reactive] public string Host { get; set; } = "127.0.0.1";
    [Reactive] public int Port { get; set; } = 8080;
    [Reactive] public string Text { get; set; } = string.Empty;
    [Reactive] public bool IsConnected { get; set; } = false;

    public ObservableCollection<Message> Messages { get; set; } = [];

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ToggleConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; set; }

    public MainWindowViewModel()
    {
        RxApp.MainThreadScheduler = DispatcherScheduler.Current;

        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessage);
        ToggleConnectionCommand = ReactiveCommand.CreateFromTask(ToggleConnection);
    }

    private async Task ToggleConnection()
    {
        if (IsConnected)
        {
            Disconnect();
        }
        else
        {
            await Connect();
        }
    }

    private async Task Connect()
    {
        if (IsConnected)
        {
            Disconnect();
        }

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(Host, Port);
            IsConnected = true;

            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

            _ = Task.Run(() => ReceiveLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            Messages.Add(new Message { Text = $"Ошибка подключения: {ex.Message}" });
            _client?.Dispose();
            _client = null;
        }
    }

    private void Disconnect()
    {
        if (_client == null) return;

        _cts.Cancel();
        _client.Close();
        _client.Dispose();
        _client = null;
        IsConnected = false;
    }

    private async Task SendMessage()
    {
        if (!IsConnected)
        {
            MessageBox.Show("Сначало подключитесь к серверу", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Text) || !_client.Connected) return;

        try
        {
            Messages.Add(new Message { Text = Text, IsOwnMessage = true });

            await TcpMessageHelper.SendMessage(_client, Text);
            Text = string.Empty;
        }
        catch (Exception ex)
        {
            Messages.Add(new Message { Text = $"Ошибка: {ex.Message}" });
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        Debug.WriteLine("[ReceiveLoop] Запуск ReceiveLoop");

        if (_client == null)
            return;

        try
        {
            while (!ct.IsCancellationRequested && _client.Connected)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    string msg = await TcpMessageHelper.ReceiveMessage(_client);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Messages.Add(new Message { Text = msg });
                    });
                    //Messages.Add(new Message { Text = msg });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
                {
                    break;
                }
            }
        }
        finally
        {
            Debug.WriteLine("[ReceiveLoop] Запуск страрт блока finally");
            if (!Application.Current.Dispatcher.HasShutdownStarted)
            {
                if (_client?.Connected == false)
                {
                    IsConnected = false;
                    Messages.Add(new Message { Text = "Соединение с сервером потеряно" });
                }
            };

            if (_client != null)
            {
                _client.Close();
                _client.Dispose();
                _client = null;
            }
        }
    }
}