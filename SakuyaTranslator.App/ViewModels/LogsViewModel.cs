using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SakuyaTranslator.App.ViewModels;

public sealed class LogsViewModel : ViewModelBase
{
    private string? _lastMessage;
    private int _lastRepeatCount = 1;

    public LogsViewModel()
    {
        ClearCommand = new RelayCommand(_ => Clear());
        Add("应用已启动。");
    }

    public ObservableCollection<string> Lines { get; } = [];
    public ICommand ClearCommand { get; }

    public void Add(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        if (Lines.Count > 0 && string.Equals(_lastMessage, message, StringComparison.Ordinal))
        {
            _lastRepeatCount++;
            Lines[0] = $"{timestamp}  {message}  (x{_lastRepeatCount})";
            return;
        }

        _lastMessage = message;
        _lastRepeatCount = 1;
        Lines.Insert(0, $"{timestamp}  {message}");
        while (Lines.Count > 300)
        {
            Lines.RemoveAt(Lines.Count - 1);
        }
    }

    private void Clear()
    {
        Lines.Clear();
        _lastMessage = null;
        _lastRepeatCount = 1;
    }
}
