using Avalonia.Controls;
using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.App.Dialogs;

public partial class ResumeDialog : Window
{
    public ResumeDialog()
    {
        InitializeComponent();
    }

    public static Task<ResumeChoice> ShowAsync(Window owner, TranslationProject project)
    {
        var dialog = new ResumeDialog();
        dialog.BodyText.Text =
            $"文件：{project.Job.FileName}\n" +
            $"进度：{project.Job.CompletedCount}/{project.Job.TotalCount}\n" +
            $"最后更新时间：{project.Job.UpdatedAt:yyyy-MM-dd HH:mm:ss}\n\n" +
            "你想继续上次进度，还是重新开始？";
        return dialog.ShowDialog<ResumeChoice>(owner);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(ResumeChoice.Cancel);

    private void ViewOnly_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(ResumeChoice.ViewOnly);

    private void Restart_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(ResumeChoice.Restart);

    private void Continue_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(ResumeChoice.Continue);
}
