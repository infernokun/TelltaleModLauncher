using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.ViewModels;

public sealed record GameSetupRequest(GameVersion? Game, string? ExePath, IReadOnlyCollection<GameVersion> Taken);

public sealed record GameSetupResult(GameVersion Game, string ExePath);

/// <summary>Everything the view model needs from the window: dialogs, pickers and the OS.</summary>
public interface IShell
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false);
    Task ShowMessageAsync(string title, string message);
    Task<string?> PickModZipAsync();
    Task<GameSetupResult?> SetupGameAsync(GameSetupRequest request);
    Task ShowModDetailsAsync(ModItemViewModel mod, ModManager manager);
    Task ShowSettingsAsync();
    Task OpenFolderAsync(string path);
    Task<bool> OpenUriAsync(Uri uri);
    void StartProcess(LaunchPlan.StartProcess plan);
}
