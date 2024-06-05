using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ServiceInterface;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace AI_Training_API;

public class MainWindowViewModel : ObservableObject
{
    public ICommand SendCommand { get; }
    public ICommand PreviousCommand { get; }

    private string _input;
    public string Input { get => _input; set => Set(ref _input, value); }

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    private MessageViewModel Status { get; } = new(ChatMessageType.Status);

    private bool _isReady = true;
    public bool IsReady { get => _isReady; set => Set(ref _isReady, value); }

    private readonly IOpenAIAPI _api;
    private string lastInput;
    
    public MainWindowViewModel(IOpenAIAPI api, IFunctionInvocationObserver functionObserver)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));

        SendCommand = new RelayCommand(async () => await Send());
        PreviousCommand = new RelayCommand(() => Input = lastInput);

        functionObserver.OnFunctionInvocation += OnFunctionInvocation;
        functionObserver.OnFunctionResult += OnFunctionResult;
        functionObserver.OnFunctionProgressUpdate += OnFunctionProgress;
    }

    private void OnFunctionProgress(object sender, string e)
    {
        UpdateStatus(e);
    }

    private void OnFunctionResult(object sender, string e)
    {
        string sessionId = _api.ActiveSessionId;
        
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            AddFunctionInfo($"Function Result:{e}\n\nSession: {sessionId}", isResult: true);
            Status.Message = "Processing";
        });
    }

    private void OnFunctionInvocation(object sender, string e)
    {
        string sessionId = _api.ActiveSessionId;

        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            AddFunctionInfo($"Function Invoke:{e}\n\nSession: {sessionId}", isResult: false);
        });
    }

    private void AddFunctionInfo(string info, bool isResult)
    {
        Messages.Add(new MessageViewModel(isResult ? ChatMessageType.FunctionResult : ChatMessageType.FunctionCall, info));
        if (Messages.Contains(Status))
        {
            Messages.Remove(Status);
            Messages.Add(Status);
        }
    }

    private void UpdateStatus(string message)
    {
        Status.Message = message;
    }

    private async Task Send()
    {
        CancellationTokenSource cts = new();

        if (!IsReady)
            return;

        IsReady = false;

        try
        {
            Messages.Add(new (ChatMessageType.User, Input));

            Status.Message = "Processing";
            Messages.Add(Status);
            
            _ = AnimateStatusElipse(cts.Token);

            string result = await _api.Prompt("Main", Input);

            Messages.Add(new (ChatMessageType.Bot, result));
        }
        catch (Exception ex) 
        {
            Messages.Add(new (ChatMessageType.Error, ex.Message));
        }
        finally
        {
            cts.Cancel();
            lastInput = Input;
            Input = string.Empty;
            Messages.Remove(Status);
            IsReady = true;
        }
    }

    private async Task AnimateStatusElipse(CancellationToken cancellationToken)
    {
        string[] statusTexts = { "", ".", "..", "..." };
        int index = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Status.Message = Status.Message.TrimEnd('.') + statusTexts[index++ % statusTexts.Length];
            await Task.Delay(300, cancellationToken);
        }
    }
}
