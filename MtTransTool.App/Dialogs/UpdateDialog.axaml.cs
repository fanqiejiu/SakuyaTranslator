using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using MtTransTool.Core.Models;

namespace MtTransTool.App.Dialogs;

public partial class UpdateDialog : Window
{
    private string _downloadUrl = "";

    public UpdateDialog()
    {
        InitializeComponent();
    }

    public static Task ShowAsync(Window owner, UpdateCheckResult result)
    {
        var dialog = new UpdateDialog();
        dialog.Apply(result);
        return dialog.ShowDialog(owner);
    }

    private void Apply(UpdateCheckResult result)
    {
        if (!result.Success)
        {
            TitleText.Text = "检查更新失败";
            MetaText.Text = result.ErrorMessage;
            ChangelogText.Text = "无法连接 GitHub 或备用 update.json，请稍后再试。";
            DownloadButton.IsEnabled = false;
            CopyButton.IsEnabled = false;
            return;
        }

        if (!result.HasUpdate || result.Latest is null)
        {
            TitleText.Text = "当前已是最新版本";
            MetaText.Text = $"当前版本：v{result.CurrentVersion}";
            ChangelogText.Text = "没有发现新版本。";
            DownloadButton.IsEnabled = false;
            CopyButton.IsEnabled = false;
            return;
        }

        _downloadUrl = string.IsNullOrWhiteSpace(result.Latest.DownloadUrl)
            ? result.Latest.ChangelogUrl
            : result.Latest.DownloadUrl;

        TitleText.Text = $"发现新版本 v{result.Latest.Version}";
        MetaText.Text = $"当前版本：v{result.CurrentVersion}    来源：{result.Latest.Source}";
        ChangelogText.Text = string.IsNullOrWhiteSpace(result.Latest.Changelog)
            ? "这个版本没有填写更新日志。"
            : result.Latest.Changelog;
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OpenDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_downloadUrl))
        {
            Process.Start(new ProcessStartInfo(_downloadUrl) { UseShellExecute = true });
        }
    }

    private async void CopyDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Clipboard is IClipboard clipboard && !string.IsNullOrWhiteSpace(_downloadUrl))
        {
            await clipboard.SetTextAsync(_downloadUrl);
        }
    }
}
