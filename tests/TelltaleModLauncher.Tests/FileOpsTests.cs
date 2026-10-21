using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

// Windows refuses to replace or delete a file another process has open, which is what
// antivirus and sync tools do for a moment after a file is written. Linux allows both, so
// these only mean something on Windows.
public class FileOpsTests
{
    [Fact]
    public async Task Replacing_a_briefly_locked_file_waits_for_the_lock()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "File locks only block replacing on Windows.");
        using var folder = new TestFolder();
        var source = Path.Combine(folder.Root, "new.json");
        var target = Path.Combine(folder.Root, "settings.json");
        File.WriteAllText(source, "new");
        File.WriteAllText(target, "old");

        var release = LockBriefly(target);
        FileOps.Move(source, target, overwrite: true);
        await release;

        Assert.Equal("new", File.ReadAllText(target));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task Deleting_a_briefly_locked_file_waits_for_the_lock()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "File locks only block deleting on Windows.");
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "mod.ttarch2");
        File.WriteAllText(path, "data");

        var release = LockBriefly(path);
        FileOps.Delete(path);
        await release;

        Assert.False(File.Exists(path));
    }

    /// <summary>Holds the file open the way a virus scanner does, then lets go after 100 ms.</summary>
    private static Task LockBriefly(string path)
    {
        var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var token = TestContext.Current.CancellationToken;
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100, token);
            }
            finally
            {
                await locked.DisposeAsync();
            }
        }, token);
    }

    [Fact]
    public void A_file_that_stays_locked_still_fails()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "File locks only block replacing on Windows.");
        using var folder = new TestFolder();
        var source = Path.Combine(folder.Root, "new.ttarch2");
        var target = Path.Combine(folder.Root, "in-use.ttarch2");
        File.WriteAllText(source, "new");
        File.WriteAllText(target, "old");

        // Like an archive the running game has open.
        using var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.ThrowsAny<Exception>(() => FileOps.Move(source, target, overwrite: true));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void A_missing_source_fails_straight_away()
    {
        using var folder = new TestFolder();
        var started = DateTime.UtcNow;

        Assert.Throws<FileNotFoundException>(() =>
            FileOps.Move(Path.Combine(folder.Root, "missing"), Path.Combine(folder.Root, "target")));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(100), "a missing file should not be retried");
    }
}
