using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettings settings;
    private readonly IShell shell;
    private ModManager? manager;
    private bool busy;

    public MainWindowViewModel(AppSettings settings, IShell shell)
    {
        this.settings = settings;
        this.shell = shell;
        AppVersion = "v" + (Assembly.GetEntryAssembly()?.GetName().Version ?? typeof(MainWindowViewModel).Assembly.GetName().Version)?.ToString(3);
        LoadGames(settings.App.Default_Game_Version);
    }

    public ObservableCollection<GameItemViewModel> Games { get; } = [];
    public ObservableCollection<ModItemViewModel> Mods { get; } = [];
    public string AppVersion { get; }

    public bool HasGames => Games.Count > 0;
    public bool HasMods => Mods.Count > 0;
    public bool ShowNoMods => CanManageMods && !HasMods;
    public bool HasGameProblem => GameProblem != null;
    public string ModCount => Mods.Count.ToString();

    public string PlayLabel => Mods.Count switch
    {
        0 => "Play",
        1 => "Play with 1 mod",
        var n => $"Play with {n} mods",
    };

    [ObservableProperty]
    public partial GameItemViewModel? SelectedGame { get; set; }

    [ObservableProperty]
    public partial bool CanManageMods { get; set; }

    [ObservableProperty]
    public partial bool CanPlay { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGameProblem))]
    public partial string? GameProblem { get; set; }

    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

    [ObservableProperty]
    public partial bool IsDragOver { get; set; }

    partial void OnSelectedGameChanged(GameItemViewModel? value)
    {
        if (value != null && settings.App.Default_Game_Version != value.Version)
        {
            settings.App.Default_Game_Version = value.Version;
            try
            {
                settings.Save();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Only the remembered game is lost.
            }
        }
        ReloadMods();
    }

    public async Task ShowStartupWarningAsync()
    {
        if (settings.LoadWarning != null)
            await shell.ShowMessageAsync("Settings were reset", settings.LoadWarning);
    }

    /// <summary>Rescans the mods folder, unless an install or removal is running.</summary>
    public void RefreshIfIdle()
    {
        if (!busy)
            ReloadMods();
    }

    [RelayCommand]
    private void Refresh() => ReloadMods();

    private void ReloadMods()
    {
        Mods.Clear();
        manager = null;
        string? problem = null;

        var game = SelectedGame;
        if (game != null)
        {
            var gameSettings = game.Settings;
            if (!gameSettings.ExeExists)
                problem = $"The game isn't at {gameSettings.Game_LocationExe} anymore. Right-click it in the list to set its location again.";
            else if (gameSettings.FindModsDirectory() is not { } modsDirectory)
                problem = "There's no Archives or Pack folder next to the game's exe, so mods can't be installed here. Right-click the game to pick its main exe.";
            else
            {
                manager = new ModManager(modsDirectory, gameSettings.Game_Location);
                foreach (var mod in manager.Scan())
                    Mods.Add(new ModItemViewModel(mod, ViewModAsync, RemoveModAsync, MoveByAsync, manager.CanSetPriority(mod)));
            }
            game.UpdateMeta(manager != null ? Mods.Count : null);
        }

        GameProblem = problem;
        CanManageMods = manager != null;
        CanPlay = game?.Settings.ExeExists == true;
        OnPropertyChanged(nameof(HasMods));
        OnPropertyChanged(nameof(ShowNoMods));
        OnPropertyChanged(nameof(ModCount));
        OnPropertyChanged(nameof(PlayLabel));
    }

    private void LoadGames(GameVersion? select)
    {
        Games.Clear();
        var configured = settings.ConfiguredGames
            .OrderByDescending(g => GameCatalog.IsSupported(g.Game_Version))
            .ThenBy(g => GameCatalog.DisplayName(g.Game_Version), StringComparer.OrdinalIgnoreCase);

        foreach (var gameSettings in configured)
        {
            var item = new GameItemViewModel(gameSettings, ChangeGameLocationAsync, RemoveGameAsync);
            item.UpdateMeta(CountMods(gameSettings));
            Games.Add(item);
        }
        OnPropertyChanged(nameof(HasGames));

        var next = Games.FirstOrDefault(g => g.Version == select) ?? Games.FirstOrDefault();
        if (next == SelectedGame)
            ReloadMods();
        else
            SelectedGame = next;
    }

    private static int? CountMods(GameVersionSettings game) =>
        game.ExeExists && game.FindModsDirectory() is { } dir ? new ModManager(dir, game.Game_Location).Scan().Count : null;

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (SelectedGame is not { } game)
            return;

        var plan = GameLauncher.Plan(game.Settings, game.Steam, OperatingSystem.IsWindows(), GameLauncher.FindOnPath);
        try
        {
            switch (plan)
            {
                case LaunchPlan.OpenUri open:
                    if (!await shell.OpenUriAsync(open.Uri))
                        await shell.ShowMessageAsync("Couldn't start the game", "Steam didn't accept the launch request. Check that Steam is installed and signed in.");
                    break;
                case LaunchPlan.StartProcess start:
                    shell.StartProcess(start);
                    break;
                case LaunchPlan.Unavailable unavailable:
                    await shell.ShowMessageAsync("Couldn't start the game", unavailable.Reason);
                    break;
            }
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            await shell.ShowMessageAsync("Couldn't start the game", e.Message);
        }
    }

    [RelayCommand]
    private async Task OpenGameFolderAsync()
    {
        if (SelectedGame is { } game && Directory.Exists(game.Settings.Game_Location))
            await shell.OpenFolderAsync(game.Settings.Game_Location);
    }

    [RelayCommand]
    private async Task AddModAsync()
    {
        if (await shell.PickModZipAsync() is { } path)
            await InstallZipAsync(path);
    }

    public async Task InstallZipAsync(string zipPath)
    {
        if (busy)
            return;
        if (manager is not { } mods || SelectedGame is not { } game)
        {
            await shell.ShowMessageAsync("Can't add the mod", "Pick a game with a mods folder first.");
            return;
        }

        busy = true;
        try
        {
            ModPackage package;
            try
            {
                package = ModManager.ReadPackage(zipPath);
            }
            catch (Exception e) when (e is ModPackageException or IOException or UnauthorizedAccessException)
            {
                await shell.ShowMessageAsync("Can't add the mod", e.Message);
                return;
            }

            var name = package.Info.ModDisplayName;
            var plan = mods.Plan(package, game.Version, Mods.Select(m => m.Mod).ToList());

            if (!plan.IsCompatible)
            {
                if (plan.ModGame is { } other)
                {
                    await shell.ShowMessageAsync("This mod is for a different game", $"'{name}' was made for {GameCatalog.DisplayName(other)}, not {game.Name}.");
                    return;
                }

                var claim = package.Info.ModCompatibility.Length > 0
                    ? $"says it's for \"{package.Info.ModCompatibility}\", which the launcher doesn't recognize"
                    : "doesn't say which game it's for";
                if (!await shell.ConfirmAsync("Unknown game", $"'{name}' {claim}. Install it for {game.Name} anyway?", "Install anyway"))
                    return;
            }

            var replacing = new List<InstalledMod>();
            if (plan.Existing is { } existing)
            {
                var oldVersion = existing.Info.ModVersion;
                var message = oldVersion == package.Info.ModVersion
                    ? $"'{name}' version {oldVersion} is already installed. Install it again?"
                    : $"'{name}' version {oldVersion} is installed. Replace it with version {package.Info.ModVersion}?";
                if (!await shell.ConfirmAsync("Replace mod", message, "Replace"))
                    return;
                replacing.Add(existing);
            }

            if (plan.Conflicts.Count > 0)
            {
                var names = string.Join(", ", plan.Conflicts.Select(c => $"'{c.Info.ModDisplayName}'"));
                var files = string.Join(", ", plan.ConflictingFiles);
                var message = $"'{name}' uses the same files as {names}: {files}.\n\nRemove {(plan.Conflicts.Count == 1 ? "that mod" : "those mods")} and install '{name}'?";
                if (!await shell.ConfirmAsync("Mods share files", message, "Remove and install", destructive: true))
                    return;
                replacing.AddRange(plan.Conflicts);
            }

            try
            {
                await Task.Run(() => mods.Install(package, replacing));
            }
            catch (Exception e) when (e is ModPackageException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                await shell.ShowMessageAsync("Couldn't install the mod", e.Message);
            }
        }
        finally
        {
            busy = false;
            ReloadMods();
        }
    }

    private async Task RemoveModAsync(ModItemViewModel mod)
    {
        if (busy || manager is not { } mods)
            return;
        if (!await shell.ConfirmAsync("Remove mod", $"Remove '{mod.Name}'? Its files are deleted from the game folder.", "Remove", destructive: true))
            return;

        await RunAsync("Couldn't remove the mod", () => mods.Remove(mod.Mod));
    }

    [RelayCommand]
    private async Task RemoveAllAsync()
    {
        if (busy || manager is not { } mods || Mods.Count == 0)
            return;

        var count = Mods.Count == 1 ? "the installed mod" : $"all {Mods.Count} mods";
        if (!await shell.ConfirmAsync("Remove all mods", $"Remove {count}? Their files are deleted from the game folder and this can't be undone.", "Remove all", destructive: true))
            return;

        var installed = Mods.Select(m => m.Mod).ToList();
        await RunAsync("Couldn't remove every mod", () => installed.ForEach(mods.Remove));
    }

    /// <summary>
    /// Moves a mod to a new place in the list and rewrites priorities to match, so the mods
    /// higher in the list load over the ones below. The json's priority is only the author's
    /// suggestion; this decides what the game actually uses.
    /// </summary>
    public async Task MoveModAsync(ModItemViewModel mod, int newIndex)
    {
        if (busy || manager is not { } mods || !mod.CanReorder)
            return;
        var order = Mods.ToList();
        var oldIndex = order.IndexOf(mod);
        newIndex = Math.Clamp(newIndex, 0, order.Count - 1);
        if (oldIndex < 0 || oldIndex == newIndex)
            return;

        order.RemoveAt(oldIndex);
        order.Insert(newIndex, mod);
        await RunAsync("Couldn't change the load order", () => mods.Reorder(order.Select(m => m.Mod).ToList()));
    }

    private Task MoveByAsync(ModItemViewModel mod, int delta) => MoveModAsync(mod, Mods.IndexOf(mod) + delta);

    private async Task RunAsync(string failureTitle, Action action)
    {
        busy = true;
        try
        {
            await Task.Run(action);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await shell.ShowMessageAsync(failureTitle, e.Message);
        }
        finally
        {
            busy = false;
            ReloadMods();
        }
    }

    private async Task ViewModAsync(ModItemViewModel mod)
    {
        if (manager is { } mods)
            await shell.ShowModDetailsAsync(mod, mods);
    }

    [RelayCommand]
    private async Task AddGameAsync()
    {
        var taken = settings.ConfiguredGames.Select(g => g.Game_Version).ToList();
        if (await shell.SetupGameAsync(new GameSetupRequest(null, null, taken)) is not { } result)
            return;

        settings.For(result.Game).SetExe(result.ExePath);
        await SaveSettingsAsync();
        LoadGames(result.Game);
    }

    private async Task ChangeGameLocationAsync(GameItemViewModel game)
    {
        if (await shell.SetupGameAsync(new GameSetupRequest(game.Version, game.Settings.Game_LocationExe, [])) is not { } result)
            return;

        game.Settings.SetExe(result.ExePath);
        await SaveSettingsAsync();
        LoadGames(game.Version);
    }

    private async Task RemoveGameAsync(GameItemViewModel game)
    {
        if (!await shell.ConfirmAsync("Remove game", $"Remove {game.Name} from the launcher? Installed mods stay in the game folder.", "Remove"))
            return;

        game.Settings.Clear();
        await SaveSettingsAsync();
        LoadGames(SelectedGame == game ? null : SelectedGame?.Version);
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            settings.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await shell.ShowMessageAsync("Couldn't save settings", $"The change applies until the launcher closes. {e.Message}");
        }
    }

    [RelayCommand]
    private Task OpenSettingsAsync() => shell.ShowSettingsAsync();
}
