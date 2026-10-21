using Avalonia.Controls;
using Avalonia.Interactivity;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(AppSettings settings) : this()
    {
        LightTheme.IsChecked = settings.App.UI_LightMode;
        DimWindow.IsChecked = settings.App.UI_WindowDefocusing;
        ConfigPath.Text = settings.FilePath;

        LightTheme.IsCheckedChanged += (_, _) =>
        {
            settings.App.UI_LightMode = LightTheme.IsChecked == true;
            App.ApplyTheme(settings.App.UI_LightMode);
            TrySave(settings);
        };
        DimWindow.IsCheckedChanged += (_, _) =>
        {
            settings.App.UI_WindowDefocusing = DimWindow.IsChecked == true;
            TrySave(settings);
        };
    }

    /// <summary>A toggle still applies for this session when the settings file can't be written.</summary>
    private static void TrySave(AppSettings settings)
    {
        try
        {
            settings.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async void OnHelp(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(AppSettings.SetupHelpLink));

    private void OnDone(object? sender, RoutedEventArgs e) => Close(true);
}
