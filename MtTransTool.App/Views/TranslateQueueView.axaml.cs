using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MtTransTool.App.Dialogs;
using MtTransTool.App.ViewModels;
using MtTransTool.Core.Models;

namespace MtTransTool.App.Views;

public partial class TranslateQueueView : UserControl
{
    public TranslateQueueView()
    {
        InitializeComponent();
    }

    private async void OpenJobDetail_Click(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TranslationJobViewModel job }
            || DataContext is not TranslateQueueViewModel vm)
        {
            return;
        }

        vm.SelectedJob = job;
        await vm.ShowSelectedJobDetailAsync();
    }

    private async void DeleteJob_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Control { DataContext: TranslationJobViewModel job }
            || DataContext is not TranslateQueueViewModel vm)
        {
            return;
        }

        await DeleteJobAsync(vm, job);
    }

    private async void DeleteSelectedJob_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TranslateQueueViewModel { SelectedJob: not null } vm)
        {
            return;
        }

        await DeleteJobAsync(vm, vm.SelectedJob);
    }

    private async Task DeleteJobAsync(TranslateQueueViewModel vm, TranslationJobViewModel job)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var confirmed = await TypedConfirmDialog.ShowDeleteAsync(owner, job.FileName);
        if (!confirmed)
        {
            return;
        }

        vm.SelectedJob = job;
        if (vm.RemoveSelectedCommand.CanExecute(null))
        {
            vm.RemoveSelectedCommand.Execute(null);
        }
    }

    private async void AddJson_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TranslateQueueViewModel vm)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择翻译文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("支持的翻译文件") { Patterns = ["*.json", "*.srt", "*.txt", "*.csv"] },
                new FilePickerFileType("MTool JSON") { Patterns = ["*.json"] },
                new FilePickerFileType("字幕文件") { Patterns = ["*.srt"] },
                new FilePickerFileType("文本文件") { Patterns = ["*.txt"] },
                new FilePickerFileType("CSV 文件") { Patterns = ["*.csv"] }
            ]
        });

        await vm.AddFilesAsync(files.Select(x => x.Path.LocalPath), project => AskResumeAsync(project));
    }

    private async void AddFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TranslateQueueViewModel vm)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择包含翻译文件的文件夹",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        var patterns = new[] { "*.json", "*.srt", "*.txt", "*.csv" };
        var paths = patterns.SelectMany(pattern => Directory.EnumerateFiles(folder.Path.LocalPath, pattern, SearchOption.AllDirectories));
        await vm.AddFilesAsync(paths, project => AskResumeAsync(project));
    }

    private async Task<ResumeChoice> AskResumeAsync(TranslationProject project)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        return owner is null
            ? ResumeChoice.Continue
            : await ResumeDialog.ShowAsync(owner, project);
    }
}
