using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

public class ModManagerTests
{
    private const GameVersion Wdc = GameVersion.The_Walking_Dead_Definitive_Series;

    private static (ModManager Manager, string GameDir, string ModsDir) NewGame(TestFolder folder)
    {
        var gameDir = Path.GetDirectoryName(folder.CreateGame())!;
        var modsDir = Path.Combine(gameDir, "Archives");
        return (new ModManager(modsDir, gameDir), gameDir, modsDir);
    }

    [Fact]
    public void Installs_files_into_the_mods_and_game_folders()
    {
        using var folder = new TestFolder();
        var (manager, gameDir, modsDir) = NewGame(folder);
        var zip = folder.CreateModZip("Load Any Level", files: ["LoadAnyLevel.ttarch2", "_resdesc_50_LoadAnyLevel.lua"], gameFiles: ["config.ini"], folder: "LoadAnyLevel/");

        manager.Install(ModManager.ReadPackage(zip));

        Assert.True(File.Exists(Path.Combine(modsDir, "LoadAnyLevel.ttarch2")));
        Assert.True(File.Exists(Path.Combine(modsDir, "_resdesc_50_LoadAnyLevel.lua")));
        Assert.True(File.Exists(Path.Combine(gameDir, "config.ini")));
        Assert.False(File.Exists(Path.Combine(modsDir, "config.ini")));
        var mod = Assert.Single(manager.Scan());
        Assert.Equal("Load Any Level", mod.Info.ModDisplayName);
        Assert.Equal(["config.ini"], mod.Info.ModFilesGameDirectory);
        Assert.False(mod.HasMissingFiles);
    }

    [Fact]
    public void Remove_deletes_the_files_and_the_info_json()
    {
        using var folder = new TestFolder();
        var (manager, gameDir, modsDir) = NewGame(folder);
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Load Any Level", gameFiles: ["config.ini"])));

        manager.Remove(manager.Scan().Single());

        Assert.Empty(Directory.GetFiles(modsDir));
        Assert.False(File.Exists(Path.Combine(gameDir, "config.ini")));
        Assert.Empty(manager.Scan());
    }

    [Fact]
    public void A_zip_without_mod_info_is_rejected()
    {
        using var folder = new TestFolder();
        var zip = folder.CreateZip("source.zip", new() { ["README.md"] = "hi", ["package.json"] = "{\"name\":\"x\"}" });

        var error = Assert.Throws<ModPackageException>(() => ModManager.ReadPackage(zip));

        Assert.Contains("source code", error.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_rejected()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "fake.zip");
        File.WriteAllText(path, "not a zip");

        Assert.Throws<ModPackageException>(() => ModManager.ReadPackage(path));
    }

    [Fact]
    public void Plan_flags_a_mod_for_another_game()
    {
        using var folder = new TestFolder();
        var (manager, _, _) = NewGame(folder);
        var package = ModManager.ReadPackage(folder.CreateModZip("Fables Fix", game: "The_Wolf_Among_Us"));

        var plan = manager.Plan(package, Wdc, []);

        Assert.False(plan.IsCompatible);
        Assert.Equal(GameVersion.The_Wolf_Among_Us, plan.ModGame);
    }

    [Fact]
    public void Plan_finds_the_installed_copy_and_mods_sharing_files()
    {
        using var folder = new TestFolder();
        var (manager, _, _) = NewGame(folder);
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Load Any Level", files: ["LoadAnyLevel.ttarch2"])));
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Other Mod", files: ["Shared.ttarch2"], infoName: "other.json")));
        var installed = manager.Scan();

        var update = ModManager.ReadPackage(folder.CreateModZip("Load Any Level", version: "4.0.0", files: ["LoadAnyLevel.ttarch2", "shared.TTARCH2"]));
        var plan = manager.Plan(update, Wdc, installed);

        Assert.True(plan.IsCompatible);
        Assert.Equal("Load Any Level", plan.Existing?.Info.ModDisplayName);
        Assert.Equal("Other Mod", Assert.Single(plan.Conflicts).Info.ModDisplayName);
        Assert.Equal(["Shared.ttarch2"], plan.ConflictingFiles);
    }

    [Fact]
    public void Two_mods_with_the_same_info_file_name_both_survive()
    {
        using var folder = new TestFolder();
        var (manager, _, _) = NewGame(folder);

        manager.Install(ModManager.ReadPackage(folder.CreateModZip("First Mod")));
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Second Mod")));

        Assert.Equal(["First Mod", "Second Mod"], manager.Scan().Select(m => m.Info.ModDisplayName).Order());
    }

    [Fact]
    public void Replacing_a_mod_removes_the_old_files()
    {
        using var folder = new TestFolder();
        var (manager, _, modsDir) = NewGame(folder);
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Load Any Level", files: ["Old.ttarch2"])));

        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Load Any Level", version: "2.0.0", files: ["New.ttarch2"])), manager.Scan());

        Assert.False(File.Exists(Path.Combine(modsDir, "Old.ttarch2")));
        var mod = Assert.Single(manager.Scan());
        Assert.Equal("2.0.0", mod.Info.ModVersion);
    }

    [Fact]
    public void Scan_reports_missing_files_and_ignores_other_json()
    {
        using var folder = new TestFolder();
        var (manager, _, modsDir) = NewGame(folder);
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Load Any Level", files: ["A.ttarch2", "B.ttarch2"])));
        File.Delete(Path.Combine(modsDir, "B.ttarch2"));
        File.WriteAllText(Path.Combine(modsDir, "settings.json"), "{\"volume\": 3}");

        var mod = Assert.Single(manager.Scan());

        Assert.Equal(["B.ttarch2"], mod.MissingFiles);
    }

    [Fact]
    public void Priority_comes_from_a_plain_text_resdesc()
    {
        using var folder = new TestFolder();
        var (manager, _, modsDir) = NewGame(folder);
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Low", files: ["_resdesc_50_Low.lua"])));
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("High", files: ["_resdesc_50_High.lua"], infoName: "high.json")));
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Locked", files: ["_resdesc_50_Locked.lua"], infoName: "locked.json")));
        File.WriteAllText(Path.Combine(modsDir, "_resdesc_50_Low.lua"), "local set = {}\nset.priority = 900\nset.gameDataPriority = 0\n");
        File.WriteAllText(Path.Combine(modsDir, "_resdesc_50_High.lua"), "set.priority=1100");
        File.WriteAllBytes(Path.Combine(modsDir, "_resdesc_50_Locked.lua"), [0x1B, 0x4C, 0x45, 0x6E, 0x01]);

        var mods = manager.Scan();

        Assert.Equal(["High", "Low", "Locked"], mods.Select(m => m.Info.ModDisplayName));
        Assert.Equal([1100, 900, (int?)null], mods.Select(m => m.Priority));
    }

    [Fact]
    public void A_game_file_the_mod_replaces_comes_back_on_remove()
    {
        using var folder = new TestFolder();
        var (manager, gameDir, _) = NewGame(folder);
        var ini = Path.Combine(gameDir, "WDC.ini");
        File.WriteAllText(ini, "original");

        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Ini Tweak", gameFiles: ["WDC.ini"])));
        Assert.Equal("contents of WDC.ini", File.ReadAllText(ini));
        Assert.Equal("original", File.ReadAllText(ini + ModManager.BackupSuffix));

        manager.Remove(manager.Scan().Single());

        Assert.Equal("original", File.ReadAllText(ini));
        Assert.False(File.Exists(ini + ModManager.BackupSuffix));
    }

    [Fact]
    public void A_mod_installed_by_an_earlier_release_is_removed_from_the_mods_folder_only()
    {
        using var folder = new TestFolder();
        var (manager, gameDir, modsDir) = NewGame(folder);
        // Earlier releases extracted everything, ModFilesGameDirectory included, into Archives.
        File.WriteAllText(Path.Combine(modsDir, "old.json"),
            """{ "ModDisplayName": "Old Mod", "ModFiles": ["Old.ttarch2"], "ModFilesGameDirectory": ["WDC.ini"] }""");
        File.WriteAllText(Path.Combine(modsDir, "Old.ttarch2"), "");
        File.WriteAllText(Path.Combine(modsDir, "WDC.ini"), "mod copy");
        File.WriteAllText(Path.Combine(gameDir, "WDC.ini"), "game copy");

        var mod = Assert.Single(manager.Scan());
        Assert.False(mod.HasMissingFiles);
        manager.Remove(mod);

        Assert.Empty(Directory.GetFiles(modsDir));
        Assert.Equal("game copy", File.ReadAllText(Path.Combine(gameDir, "WDC.ini")));
    }

    [Fact]
    public void Extra_files_in_the_zip_are_removed_with_the_mod()
    {
        using var folder = new TestFolder();
        var (manager, _, modsDir) = NewGame(folder);
        var zip = folder.CreateZip("extras.zip", new()
        {
            ["modinfo.json"] = """{ "ModDisplayName": "Extras", "ModFiles": ["Extras.ttarch2"] }""",
            ["Extras.ttarch2"] = "",
            ["readme.txt"] = "read me",
            ["variant/modinfo.json"] = """{ "ModDisplayName": "Extras (variant)", "ModFiles": ["Variant.ttarch2"] }""",
        });

        manager.Install(ModManager.ReadPackage(zip));
        var mod = Assert.Single(manager.Scan());
        Assert.True(File.Exists(Path.Combine(modsDir, "readme.txt")));
        Assert.Empty(Directory.GetDirectories(modsDir));

        manager.Remove(mod);
        Assert.Empty(Directory.GetFiles(modsDir));
    }

    [Fact]
    public void Zip_names_are_matched_to_the_json_spelling_and_backslashes_are_folders()
    {
        using var folder = new TestFolder();
        var (manager, gameDir, modsDir) = NewGame(folder);
        var zip = folder.CreateZip("odd.zip", new()
        {
            ["modinfo.json"] = """{ "ModDisplayName": "Odd", "ModFiles": ["Odd.ttarch2"], "ModFilesGameDirectory": ["WDC.ini"] }""",
            ["Pack\\odd.TTARCH2"] = "",
            ["wdc.ini"] = "",
        });

        manager.Install(ModManager.ReadPackage(zip));

        Assert.True(File.Exists(Path.Combine(modsDir, "Odd.ttarch2")));
        Assert.True(File.Exists(Path.Combine(gameDir, "WDC.ini")));
        Assert.False(Assert.Single(manager.Scan()).HasMissingFiles);
    }

    [Fact]
    public void Mod_info_accepts_numbers_and_skips_non_mod_json()
    {
        var mod = Mod.TryParse("""{ "ModDisplayName": "X", "ModVersion": 1.5, "ModFiles": ["dir/a.ttarch2"], }""");

        Assert.NotNull(mod);
        Assert.Equal("1.5", mod.ModVersion);
        Assert.Equal(["a.ttarch2"], mod.ModFiles);
        Assert.Null(Mod.TryParse("""{ "name": "package" }"""));
        Assert.Null(Mod.TryParse("not json"));
        Assert.Null(Mod.TryParse("""{ "ModDisplayName": "A", "ModDisplayName": "B" }"""));
    }
}
