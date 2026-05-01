using Avalonia.Controls;
using System.Diagnostics;

namespace SakuyaTranslator.App.Views;

public partial class ApiConfigView : UserControl
{
    public ApiConfigView()
    {
        InitializeComponent();
    }

    private void OpenUrl_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url } || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Keep the API page alive even if Windows cannot resolve the default browser.
        }
    }
}
