using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;

namespace TelltaleModLauncher.Tests;

public class MainWindowViewModelTests
{
    private const GameVersion Wdc = GameVersion.The_Walking_Dead_Definitive_Series;

    private static (MainWindowViewModel Vm, FakeShell Shell, AppSettings Settings, string ModsDir) Setup(TestFolder folder)
    {
        var exe = folder.CreateGame();
        var settings = AppSettings.Load(folder.ConfigPath);
        settings.For(Wdc).SetExe(exe);
        settings.Save();
        var shell = new FakeShell();
        return (new MainWindowViewModel(settings, shell), shell, settings, Path.Combine(Path.GetDirectoryName(exe)!, "Archives"));
    }

    [Fact]
    public void Starts_on_the_saved_game()
    {
        using var folder = new TestFolder();
        var (vm, _, _, _) = Setup(folder);

        Assert.True(vm.HasGames);
        Assert.Equal(Wdc, vm.SelectedGame?.Version);
        Assert.True(vm.CanManageMods);
        Assert.True(vm.ShowNoMods);
        Assert.Equal("Play", vm.PlayLabel);
        Assert.Equal("Supported · 0 mods", vm.SelectedGame?.Meta);
    }

    [Fact]
    public void With_no_games_it_shows_the_first_run_state()
    {
        using var folder = new TestFolder();

        var vm = new MainWindowViewModel(AppSettings.Load(folder.ConfigPath), new FakeShell());

        Assert.False(vm.HasGames);
        Assert.Null(vm.SelectedGame);
        Assert.False(vm.CanManageMods);
    }

    [Fact]
    public async Task Installing_a_zip_adds_it_to_the_list()
    {
        using var folder = new TestFolder();
        var (vm, shell, _, modsDir) = Setup(folder);

        await vm.InstallZipAsync(folder.CreateModZip("Load Any Level", version: "4.0.0"));

        var mod = Assert.Single(vm.Mods);
        Assert.Equal("Load Any Level", mod.Name);
        Assert.Equal("by tester · v4.0.0 · 1 file", mod.Meta);
        Assert.Equal("Play with 1 mod", vm.PlayLabel);
        Assert.True(File.Exists(Path.Combine(modsDir, "LoadAnyLevel.ttarch2")));
        Assert.Empty(shell.Messages);
    }

    [Fact]
    public async Task A_mod_for_another_game_is_not_installed()
    {
        using var folder = new TestFolder();
        var (vm, shell, _, _) = Setup(folder);

        await vm.InstallZipAsync(folder.CreateModZip("Fables Fix", game: "The_Wolf_Among_Us"));

        Assert.Empty(vm.Mods);
        Assert.Contains("The Wolf Among Us", Assert.Single(shell.Messages));
    }

    [Fact]
    public async Task Declining_a_conflict_changes_nothing_and_accepting_swaps_the_mods()
    {
        using var folder = new TestFolder();
        var (vm, shell, _, _) = Setup(folder);
        await vm.InstallZipAsync(folder.CreateModZip("First Mod", files: ["Shared.ttarch2"]));
        var second = folder.CreateModZip("Second Mod", files: ["Shared.ttarch2"], infoName: "second.json");

        shell.ConfirmAnswer = false;
        await vm.InstallZipAsync(second);
        Assert.Equal(["First Mod"], vm.Mods.Select(m => m.Name));
        Assert.Contains("Shared.ttarch2", shell.Confirmations.Last());

        shell.ConfirmAnswer = true;
        await vm.InstallZipAsync(second);
        Assert.Equal(["Second Mod"], vm.Mods.Select(m => m.Name));
    }

    [Fact]
    public async Task Remove_all_clears_the_folder_after_confirming()
    {
        using var folder = new TestFolder();
        var (vm, shell, _, modsDir) = Setup(folder);
        await vm.InstallZipAsync(folder.CreateModZip("First Mod"));
        await vm.InstallZipAsync(folder.CreateModZip("Second Mod"));

        await vm.RemoveAllCommand.ExecuteAsync(null);

        Assert.Empty(vm.Mods);
        Assert.Empty(Directory.GetFiles(modsDir));
        Assert.Contains("all 2 mods", shell.Confirmations.Last());
    }

    [Fact]
    public async Task Adding_a_game_saves_it_and_selects_it()
    {
        using var folder = new TestFolder();
        var (vm, shell, settings, _) = Setup(folder);
        var exe = folder.CreateGame("Fables", "TheWolfAmongUs.exe", "Pack");
        shell.SetupResult = new GameSetupResult(GameVersion.The_Wolf_Among_Us, exe);

        await vm.AddGameCommand.ExecuteAsync(null);

        Assert.Equal([Wdc], shell.SetupRequests.Single().Taken);
        Assert.Equal(2, vm.Games.Count);
        Assert.Equal(GameVersion.The_Wolf_Among_Us, vm.SelectedGame?.Version);
        Assert.Equal("Untested · 0 mods", vm.SelectedGame?.Meta);
        var reloaded = AppSettings.Load(settings.FilePath);
        Assert.Equal(exe, reloaded.For(GameVersion.The_Wolf_Among_Us).Game_LocationExe);
        Assert.Equal(GameVersion.The_Wolf_Among_Us, reloaded.App.Default_Game_Version);
    }

    [Fact]
    public async Task Removing_a_game_keeps_its_mods_on_disk()
    {
        using var folder = new TestFolder();
        var (vm, _, _, modsDir) = Setup(folder);
        await vm.InstallZipAsync(folder.CreateModZip("Load Any Level"));

        await vm.SelectedGame!.RemoveCommand.ExecuteAsync(null);

        Assert.False(vm.HasGames);
        Assert.NotEmpty(Directory.GetFiles(modsDir));
    }

    [Fact]
    public void A_game_that_moved_explains_why_nothing_works()
    {
        using var folder = new TestFolder();
        var settings = AppSettings.Load(folder.ConfigPath);
        settings.For(Wdc).Game_LocationExe = Path.Combine(folder.Root, "gone", "WDC.exe");
        settings.For(Wdc).Game_Location = Path.Combine(folder.Root, "gone");

        var vm = new MainWindowViewModel(settings, new FakeShell());

        Assert.False(vm.CanPlay);
        Assert.False(vm.CanManageMods);
        Assert.Contains("anymore", vm.GameProblem);
        Assert.Equal("Supported · Not found", vm.SelectedGame?.Meta);
    }
}
