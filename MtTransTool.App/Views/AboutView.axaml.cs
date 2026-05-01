using System.Diagnostics;
using Avalonia.Controls;
using MtTransTool.App.Dialogs;
using MtTransTool.App.ViewModels;

namespace MtTransTool.App.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    private async void CheckUpdates_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AboutViewModel vm)
        {
            return;
        }

        var result = await vm.CheckForUpdatesAsync();
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
        {
            await UpdateDialog.ShowAsync(owner, result);
        }
    }

    private void OpenData_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AboutViewModel vm)
        {
            OpenFolder(vm.DataDirectory);
        }
    }

    private void OpenLogs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AboutViewModel vm)
        {
            OpenFolder(vm.LogsDirectory);
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
