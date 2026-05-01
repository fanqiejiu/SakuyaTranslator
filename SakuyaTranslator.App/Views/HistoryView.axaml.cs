using Avalonia.Controls;
using SakuyaTranslator.App.Dialogs;
using SakuyaTranslator.App.ViewModels;

namespace SakuyaTranslator.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private async void DeleteRecord_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        if (sender is not Control { DataContext: TranslationJobViewModel job }
            || DataContext is not HistoryViewModel history)
        {
            return;
        }

        var confirmed = await ConfirmDialog.ShowAsync(
            owner,
            "确定要删除这个任务记录吗？",
            "这只会从历史记录中移除，不会删除原始 JSON 文件。",
            "删除记录");

        if (confirmed)
        {
            history.Remove(job);
        }
    }

    private async void ContinueRecord_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: TranslationJobViewModel job }
            || DataContext is not HistoryViewModel history)
        {
            return;
        }

        await history.ContinueAsync(job);
    }
}
