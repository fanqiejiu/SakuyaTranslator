using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MtTransTool.App.ViewModels;
using MtTransTool.App.Views;
using MtTransTool.Core.Models;
using MtTransTool.Core.Services;

namespace MtTransTool.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = new PortablePaths();
            paths.EnsureCreated();

            var store = new JsonFileStore();
            var settings = store.LoadOrCreate(paths.SettingsPath, new AppSettings());
            var apiProfiles = store.LoadOrCreate(paths.ApiProfilesPath, new List<ApiProfile> { new() });
            var localization = new LocalizationService(paths);
            localization.Load(settings.UiCulture);
            ApplyLocalizationResources(localization);
            RequestedThemeVariant = ThemeVariantFrom(settings.ThemeMode);

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(paths, store, settings, apiProfiles, localization)
            };
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static ThemeVariant? ThemeVariantFrom(string themeMode)
    {
        return themeMode switch
        {
            "浅色" => ThemeVariant.Light,
            "深色" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void ApplyLocalizationResources(LocalizationService localization)
    {
        foreach (var (key, value) in localization.Strings)
        {
            Resources[key] = value;
        }
    }
}
