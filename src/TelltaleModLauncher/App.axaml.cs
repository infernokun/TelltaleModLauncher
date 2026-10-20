using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using TelltaleModLauncher.Core;
using TelltaleModLauncher.Views;

namespace TelltaleModLauncher;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettings.Load(AppSettings.DefaultFilePath);
            ApplyTheme(settings.App.UI_LightMode);
            desktop.MainWindow = MainWindow.Create(settings);
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(bool light)
    {
        if (Current != null)
            Current.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
