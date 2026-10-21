using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;

namespace TelltaleModLauncher.Tests;

/// <summary>Answers dialogs from canned values and records what was shown.</summary>
public sealed class FakeShell : IShell
{
    public bool ConfirmAnswer { get; set; } = true;
    public string? ZipToPick { get; set; }
    public GameSetupResult? SetupResult { get; set; }
    public List<string> Confirmations { get; } = [];
    public List<string> Messages { get; } = [];
    public List<Uri> OpenedUris { get; } = [];
    public List<GameSetupRequest> SetupRequests { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false)
    {
        Confirmations.Add(message);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<string?> PickModZipAsync() => Task.FromResult(ZipToPick);

    public Task<GameSetupResult?> SetupGameAsync(GameSetupRequest request)
    {
        SetupRequests.Add(request);
        return Task.FromResult(SetupResult);
    }

    public Task ShowModDetailsAsync(ModItemViewModel mod, ModManager manager) => Task.CompletedTask;

    public Task ShowSettingsAsync() => Task.CompletedTask;

    public Task OpenFolderAsync(string path) => Task.CompletedTask;

    public Task<bool> OpenUriAsync(Uri uri)
    {
        OpenedUris.Add(uri);
        return Task.FromResult(true);
    }

    public void StartProcess(LaunchPlan.StartProcess plan)
    {
    }
}
