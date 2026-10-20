using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;

namespace TelltaleModLauncher.Views;

/// <summary>Opens the launcher's dialogs over the main window, dimming it when that setting is on.</summary>
public sealed class WindowShell(Window owner, AppSettings settings) : IShell
{
    public Action<bool>? Dimmed { get; set; }

    private async Task<T?> ModalAsync<T>(Window dialog)
    {
        var dim = settings.App.UI_WindowDefocusing;
        if (dim)
            Dimmed?.Invoke(true);
        try
        {
            return await dialog.ShowDialog<T?>(owner);
        }
        finally
        {
            if (dim)
                Dimmed?.Invoke(false);
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) =>
        await ModalAsync<bool>(new MessageWindow(title, message, confirmText, "Cancel", destructive));

    public Task ShowMessageAsync(string title, string message) =>
        ModalAsync<bool>(new MessageWindow(title, message, "OK", null, false));

    public async Task<string?> PickModZipAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add a mod",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Mod zip files") { Patterns = ["*.zip"] }],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public Task<GameSetupResult?> SetupGameAsync(GameSetupRequest request) =>
        ModalAsync<GameSetupResult>(new GameSetupWindow(request));

    public Task ShowModDetailsAsync(ModItemViewModel mod, ModManager manager) =>
        ModalAsync<bool>(new ModDetailsWindow(mod, manager));

    public Task ShowSettingsAsync() => ModalAsync<bool>(new SettingsWindow(settings));

    public async Task OpenFolderAsync(string path) =>
        await owner.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));

    public Task<bool> OpenUriAsync(Uri uri) => owner.Launcher.LaunchUriAsync(uri);

    public void StartProcess(LaunchPlan.StartProcess plan)
    {
        var info = new ProcessStartInfo(plan.FileName) { WorkingDirectory = plan.WorkingDirectory, UseShellExecute = false };
        foreach (var argument in plan.Arguments)
            info.ArgumentList.Add(argument);
        Process.Start(info)?.Dispose();
    }
}
