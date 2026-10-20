using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

public class SteamTests
{
    /// <summary>Lays out a Steam library with one installed app. Returns the exe path.</summary>
    internal static string CreateSteamGame(TestFolder folder, string appId = "1449690", string installDir = "The Walking Dead The Telltale Definitive Series")
    {
        var steamapps = Path.Combine(folder.Root, "Steam", "steamapps");
        Directory.CreateDirectory(steamapps);
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_228280.acf"), """
            "AppState"
            {
                "appid"     "228280"
                "name"      "Some Other Game"
                "installdir"        "Other"
            }
            """);
        File.WriteAllText(Path.Combine(steamapps, $"appmanifest_{appId}.acf"), $$"""
            "AppState"
            {
                "appid"     "{{appId}}"
                "Universe"      "1"
                "name"      "The Walking Dead: The Telltale Definitive Series"
                "installdir"        "{{installDir}}"
                "UserConfig"
                {
                    "name"      "ignored"
                }
            }
            """);
        return folder.CreateGame(Path.Combine("Steam", "steamapps", "common", installDir, "bin"));
    }

    [Fact]
    public void Finds_the_app_from_an_exe_inside_a_library()
    {
        using var folder = new TestFolder();
        var exe = CreateSteamGame(folder);

        var app = Steam.FindApp(exe);

        Assert.NotNull(app);
        Assert.Equal("1449690", app.AppId);
        Assert.Equal("The Walking Dead: The Telltale Definitive Series", app.Name);
        Assert.Equal(Path.Combine(folder.Root, "Steam"), app.LibraryPath);
    }

    [Fact]
    public void An_exe_outside_steam_has_no_app()
    {
        using var folder = new TestFolder();

        Assert.Null(Steam.FindApp(folder.CreateGame()));
    }

    [Theory]
    [InlineData("1449690_library_hero.jpg")]
    [InlineData("1449690/library_hero.jpg")]
    [InlineData("1449690/0a1b2c/library_hero.jpg")]
    public void Finds_library_artwork_in_either_cache_layout(string relative)
    {
        using var folder = new TestFolder();
        var app = Steam.FindApp(CreateSteamGame(folder))!;
        var art = Path.Combine(app.LibraryPath, "appcache", "librarycache", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(art)!);
        File.WriteAllText(art, "");

        Assert.Equal(art, Steam.FindArtwork(app, []));
    }

    [Fact]
    public void Windows_runs_the_exe_directly()
    {
        using var folder = new TestFolder();
        var game = new GameVersionSettings();
        game.SetExe(CreateSteamGame(folder));

        var plan = GameLauncher.Plan(game, Steam.FindApp(game.Game_LocationExe), isWindows: true, _ => null);

        var start = Assert.IsType<LaunchPlan.StartProcess>(plan);
        Assert.Equal(game.Game_LocationExe, start.FileName);
        Assert.Empty(start.Arguments);
    }

    [Fact]
    public void Linux_starts_a_steam_copy_through_steam()
    {
        using var folder = new TestFolder();
        var game = new GameVersionSettings();
        game.SetExe(CreateSteamGame(folder));

        var plan = GameLauncher.Plan(game, Steam.FindApp(game.Game_LocationExe), isWindows: false, _ => "/usr/bin/wine");

        Assert.Equal(new Uri("steam://rungameid/1449690"), Assert.IsType<LaunchPlan.OpenUri>(plan).Uri);
    }

    [Fact]
    public void Linux_falls_back_to_wine_then_explains()
    {
        using var folder = new TestFolder();
        var game = new GameVersionSettings();
        game.SetExe(folder.CreateGame());

        var wine = Assert.IsType<LaunchPlan.StartProcess>(GameLauncher.Plan(game, null, false, _ => "/usr/bin/wine"));
        Assert.Equal("/usr/bin/wine", wine.FileName);
        Assert.Equal([game.Game_LocationExe], wine.Arguments);

        Assert.IsType<LaunchPlan.Unavailable>(GameLauncher.Plan(game, null, false, _ => null));
    }

    [Fact]
    public void A_moved_game_cannot_launch()
    {
        var game = new GameVersionSettings { Game_LocationExe = "/nowhere/WDC.exe", Game_Location = "/nowhere" };

        Assert.IsType<LaunchPlan.Unavailable>(GameLauncher.Plan(game, null, true, _ => null));
    }
}
