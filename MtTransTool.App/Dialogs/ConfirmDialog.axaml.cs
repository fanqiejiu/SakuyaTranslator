using Avalonia.Controls;

namespace MtTransTool.App.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public static Task<bool> ShowAsync(
        Window owner,
        string title,
        string body,
        string confirmText,
        bool showCancel = true)
    {
        var dialog = new ConfirmDialog();
        dialog.TitleText.Text = title;
        dialog.BodyText.Text = body;
        dialog.ConfirmButton.Content = confirmText;
        dialog.CancelButton.IsVisible = showCancel;
        return dialog.ShowDialog<bool>(owner);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private void Confirm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
}
