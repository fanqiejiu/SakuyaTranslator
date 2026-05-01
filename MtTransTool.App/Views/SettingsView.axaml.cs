using Avalonia.Controls;
using MtTransTool.App.Dialogs;
using MtTransTool.App.ViewModels;

namespace MtTransTool.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.RestartRequired -= Settings_RestartRequired;
            vm.RestartRequired += Settings_RestartRequired;
        }
    }

    private async void Settings_RestartRequired(object? sender, EventArgs e)
    {
        if (sender is not SettingsViewModel vm)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        await ConfirmDialog.ShowAsync(
            owner,
            vm.RestartDialogTitle,
            vm.RestartDialogBody,
            vm.RestartDialogButton,
            showCancel: false);
    }
}
