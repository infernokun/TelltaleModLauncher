using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;

namespace TelltaleModLauncher.Views;

public sealed record GameOption(GameVersion Version, string Name, string Note);

/// <summary>Picks a game and its exe, for a new game or to move an existing one.</summary>
public partial class GameSetupWindow : Window
{
    public GameSetupWindow()
    {
        InitializeComponent();
    }

    public GameSetupWindow(GameSetupRequest request) : this()
    {
        var options = GameCatalog.All
            .Where(g => g == request.Game || !request.Taken.Contains(g))
            .OrderByDescending(GameCatalog.IsSupported)
            .ThenBy(GameCatalog.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GameOption(g, GameCatalog.DisplayName(g), GameCatalog.IsSupported(g) ? "Supported" : "Untested"))
            .ToList();

        GamePicker.ItemsSource = options;
        GamePicker.SelectedItem = options.FirstOrDefault(o => o.Version == request.Game) ?? options.FirstOrDefault();

        if (request.Game != null)
        {
            Title = "Game location";
            Heading.Text = "GAME LOCATION";
            GamePicker.IsEnabled = false;
            SaveButton.Content = "Save";
        }

        if (request.ExePath is { } exe && exe != GameVersionSettings.Undefined)
            ExePath.Text = exe;

        ExePath.TextChanged += (_, _) => Validate();
        Validate();
    }

    private string? iconPath;

    private string Exe => ExePath.Text?.Trim().Trim('"') ?? "";

    private void Validate()
    {
        string message;
        var ok = false;

        if (Exe.Length == 0)
            message = "Pick the game's .exe file.";
        else if (!File.Exists(Exe))
            message = "There's no file at that path.";
        else if (GameVersionSettings.FindModsDirectory(Path.GetDirectoryName(Exe)) is not { } mods)
            message = "There's no Archives or Pack folder next to this file. Pick the game's main .exe.";
        else
        {
            ok = true;
            message = $"Found the {Path.GetFileName(mods)} folder.";
            if (!OperatingSystem.IsWindows() && Steam.FindApp(Exe) is { } app)
                message += $" Play will start it through Steam (app {app.AppId}).";
        }

        Status.Text = message;
        Status.Classes.Set("muted", ok);
        Status.Classes.Set("warning", !ok && Exe.Length > 0);
        StatusIcon.IsVisible = Exe.Length > 0 && !ok;
        SaveButton.IsEnabled = ok;

        // The exe's own icon confirms the right file was picked. Only reread it when the path changes.
        var exe = ok ? Exe : null;
        if (exe != iconPath)
        {
            iconPath = exe;
            ExeIconPreview.Source = exe == null ? null : GameItemViewModel.LoadIcon(exe);
        }
        ExeIconPreview.IsVisible = ExeIconPreview.Source != null;
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        IStorageFolder? start = null;
        var dir = Exe.Length > 0 ? Path.GetDirectoryName(Exe) : null;
        if (dir != null && Directory.Exists(dir))
            start = await StorageProvider.TryGetFolderFromPathAsync(dir);

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select the game's executable",
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter = [new FilePickerFileType("Programs") { Patterns = ["*.exe"] }, FilePickerFileTypes.All],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            ExePath.Text = path;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (GamePicker.SelectedItem is GameOption option && SaveButton.IsEnabled)
            Close(new GameSetupResult(option.Version, Path.GetFullPath(Exe)));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
