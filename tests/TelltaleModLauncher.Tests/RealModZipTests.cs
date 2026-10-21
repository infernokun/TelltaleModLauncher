using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

/// <summary>
/// Installs real mod releases. The zips belong to their authors and aren't in the repo, so this
/// only runs when REAL_MOD_ZIPS names a folder of them.
/// </summary>
public class RealModZipTests
{
    [Fact]
    public void Every_zip_installs_with_its_listed_files_and_no_junk()
    {
        var dir = Environment.GetEnvironmentVariable("REAL_MOD_ZIPS");
        Assert.SkipUnless(!string.IsNullOrEmpty(dir) && Directory.Exists(dir), "Set REAL_MOD_ZIPS to a folder of mod zips.");

        foreach (var zip in Directory.GetFiles(dir!, "*.zip").Order())
        {
            using var folder = new TestFolder();
            var gameDir = Path.GetDirectoryName(folder.CreateGame())!;
            var manager = new ModManager(Path.Combine(gameDir, "Archives"), gameDir);

            var package = ModManager.ReadPackage(zip);
            Assert.True(manager.Plan(package, GameVersion.The_Walking_Dead_Definitive_Series, []).IsCompatible, $"{zip}: {package.Info.ModCompatibility}");
            manager.Install(package);

            var mod = Assert.Single(manager.Scan());
            Assert.False(mod.HasMissingFiles, $"{zip}: missing {string.Join(", ", mod.MissingFiles)}");
            Assert.DoesNotContain(Directory.GetFiles(manager.ModsDirectory, "*", SearchOption.AllDirectories), f => ModManager.IsJunk(f));

            manager.Remove(mod);
            Assert.Empty(Directory.GetFiles(manager.ModsDirectory, "*", SearchOption.AllDirectories));
        }
    }
}
