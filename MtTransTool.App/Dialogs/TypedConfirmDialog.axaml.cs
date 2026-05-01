using Avalonia.Controls;
using Avalonia.Media;

namespace MtTransTool.App.Dialogs;

public partial class TypedConfirmDialog : Window
{
    private const string RequiredText = "确认删除";

    public TypedConfirmDialog()
    {
        InitializeComponent();
    }

    public static Task<bool> ShowDeleteAsync(Window owner, string fileName)
    {
        var dialog = new TypedConfirmDialog();
        dialog.TitleText.Text = "删除这个 JSON 任务？";
        dialog.FileNameText.Text = fileName;
        dialog.InstructionText.Text = $"请输入“{RequiredText}”以继续：";
        dialog.ConfirmInput.Watermark = RequiredText;
        dialog.UpdateInputState();
        return dialog.ShowDialog<bool>(owner);
    }

    private void ConfirmInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateInputState();
    }

    private void UpdateInputState()
    {
        ConfirmButton.IsEnabled = string.Equals(
            ConfirmInput.Text?.Trim(),
            RequiredText,
            StringComparison.Ordinal);

        if (ConfirmButton.IsEnabled)
        {
            MatchHintText.Text = "输入正确，可以删除。";
            MatchHintText.Foreground = new SolidColorBrush(Color.Parse("#15803D"));
        }
        else
        {
            MatchHintText.Text = $"需要完整输入：{RequiredText}";
            MatchHintText.Foreground = new SolidColorBrush(Color.Parse("#9F1239"));
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private void Confirm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
}
