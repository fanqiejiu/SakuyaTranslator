using Avalonia.Controls;
using Avalonia.Input;
using MtTransTool.App.Dialogs;
using MtTransTool.App.ViewModels;

namespace MtTransTool.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        await vm.CheckUpdatesOnStartupAsync();
        if (vm.AboutPage.LastResult?.HasUpdate == true && vm.AboutPage.LastResult.Latest is not null)
        {
            await UpdateDialog.ShowAsync(this, vm.AboutPage.LastResult);
        }
    }
}
