using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

public class AppSettingsTests
{
    [Fact]
    public void A_missing_file_is_created_with_every_game_unset()
    {
        using var folder = new TestFolder();

        var settings = AppSettings.Load(folder.ConfigPath);

        Assert.True(File.Exists(folder.ConfigPath));
        Assert.Equal(GameCatalog.All.Count, settings.Games.Count);
        Assert.Empty(settings.ConfiguredGames);
        Assert.True(settings.App.UI_WindowDefocusing);
    }

    [Fact]
    public void Reads_the_file_written_by_earlier_releases()
    {
        using var folder = new TestFolder();
        Directory.CreateDirectory(Path.GetDirectoryName(folder.ConfigPath)!);
        // Shape written by v0.6.5, computed properties included.
        File.WriteAllText(folder.ConfigPath, """
            [
              { "Default_Game_Version": 7, "UI_LightMode": true, "UI_WindowDefocusing": false },
              [
                { "Game_Version": 0, "Game_Location": "C:\\Games\\WDC", "Game_LocationExe": "C:\\Games\\WDC\\WDC.exe",
                  "Game_LibTelltale_GameID": "wdc", "Game_Location_Mods": "" },
                { "Game_Version": 7, "Game_Location": "Undefined", "Game_LocationExe": "Undefined",
                  "Game_LibTelltale_GameID": "fables", "Game_Location_Mods": "" }
              ]
            ]
            """);

        var settings = AppSettings.Load(folder.ConfigPath);

        Assert.Equal(GameVersion.The_Wolf_Among_Us, settings.App.Default_Game_Version);
        Assert.True(settings.App.UI_LightMode);
        Assert.False(settings.App.UI_WindowDefocusing);
        Assert.Equal(@"C:\Games\WDC\WDC.exe", settings.For(GameVersion.The_Walking_Dead_Definitive_Series).Game_LocationExe);
        Assert.Equal(GameCatalog.All.Count, settings.Games.Count);
        Assert.Single(settings.ConfiguredGames);
    }

    [Fact]
    public void Saves_and_loads_back()
    {
        using var folder = new TestFolder();
        var exe = folder.CreateGame();
        var settings = AppSettings.Load(folder.ConfigPath);
        settings.For(GameVersion.Batman_The_Enemy_Within).SetExe(exe);
        settings.App.UI_LightMode = true;
        settings.Save();

        var reloaded = AppSettings.Load(folder.ConfigPath);

        var game = Assert.Single(reloaded.ConfiguredGames);
        Assert.Equal(GameVersion.Batman_The_Enemy_Within, game.Game_Version);
        Assert.Equal(exe, game.Game_LocationExe);
        Assert.Equal(Path.GetDirectoryName(exe), game.Game_Location);
        Assert.True(reloaded.App.UI_LightMode);
    }

    [Fact]
    public void A_corrupt_file_is_backed_up_and_replaced()
    {
        using var folder = new TestFolder();
        Directory.CreateDirectory(Path.GetDirectoryName(folder.ConfigPath)!);
        File.WriteAllText(folder.ConfigPath, "{ not json");

        var settings = AppSettings.Load(folder.ConfigPath);

        Assert.NotNull(settings.LoadWarning);
        Assert.Equal("{ not json", File.ReadAllText(folder.ConfigPath + ".bak"));
        Assert.Empty(settings.ConfiguredGames);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{}, [{ \"Game_Version\": 0, \"Game_Version\": 1 }]]")]
    [InlineData("[{ \"UI_LightMode\": \"yes\" }]")]
    public void Odd_files_start_fresh_instead_of_crashing(string json)
    {
        using var folder = new TestFolder();
        Directory.CreateDirectory(Path.GetDirectoryName(folder.ConfigPath)!);
        File.WriteAllText(folder.ConfigPath, json);

        var settings = AppSettings.Load(folder.ConfigPath);

        Assert.NotNull(settings.LoadWarning);
        Assert.Equal(GameCatalog.All.Count, settings.Games.Count);
    }

    [Theory]
    [InlineData("Archives")]
    [InlineData("archives")]
    [InlineData("Packs")]
    [InlineData("Pack")]
    public void Finds_the_mods_folder_whatever_its_case(string folderName)
    {
        using var folder = new TestFolder();
        var exe = folder.CreateGame(modsFolder: folderName);

        var found = GameVersionSettings.FindModsDirectory(Path.GetDirectoryName(exe));

        Assert.Equal(folderName, Path.GetFileName(found));
    }
}
