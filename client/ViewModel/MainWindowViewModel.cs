using client.Model;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Concurrency;

namespace client.ViewModel;

public class MainWindowViewModel : ReactiveObject
{
    private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    [Reactive] public string Text { get; set; } = string.Empty;

    public ObservableCollection<Message> Messages { get; set; } = [];

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; set; }

    public MainWindowViewModel()
    {
        RxApp.MainThreadScheduler = DispatcherScheduler.Current;

        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessage);

        _socket.Connect("127.0.0.1", 8080);
    }

    private async Task SendMessage()
    {
        var message = new Message { Text = Text };
        Messages.Add(message);



        Text = string.Empty;
    }
}
