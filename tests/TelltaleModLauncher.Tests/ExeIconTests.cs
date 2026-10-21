using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

public class ExeIconTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static string Write(TestFolder folder, byte[] exe)
    {
        var path = Path.Combine(folder.Root, "Game.exe");
        File.WriteAllBytes(path, exe);
        return path;
    }

    [Fact]
    public void Returns_the_largest_png_image()
    {
        using var folder = new TestFolder();
        byte[] small = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9];

        var icon = ExeIcon.TryRead(Write(folder, FakeExe.WithIcon((16, small), (256, Png), (48, small))));

        Assert.Equal(Png, icon);
    }

    [Fact]
    public void Wraps_a_bitmap_image_as_an_ico_file()
    {
        using var folder = new TestFolder();
        byte[] dib = [40, 0, 0, 0, 7, 7, 7];

        var icon = ExeIcon.TryRead(Write(folder, FakeExe.WithIcon((32, dib))))!;

        Assert.Equal([0, 0, 1, 0, 1, 0], icon[..6]);
        Assert.Equal(32, icon[6]);
        Assert.Equal(dib.Length, BitConverter.ToInt32(icon, 14));
        Assert.Equal(22, BitConverter.ToInt32(icon, 18));
        Assert.Equal(dib, icon[22..]);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x4D, 0x5A })]
    [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 2, 1, 1 })]
    public void Files_without_an_icon_return_null(byte[] bytes)
    {
        using var folder = new TestFolder();

        Assert.Null(ExeIcon.TryRead(Write(folder, bytes)));
        Assert.Null(ExeIcon.TryRead(Path.Combine(folder.Root, "missing.exe")));
    }

    [Fact]
    public void A_truncated_exe_returns_null()
    {
        using var folder = new TestFolder();
        var exe = FakeExe.WithIcon((256, Png));

        Assert.Null(ExeIcon.TryRead(Write(folder, exe[..(exe.Length - 20)])));
    }

    /// <summary>Set REAL_EXE to a game exe to check the icon reads from a real file.</summary>
    [Fact]
    public void Reads_a_real_exe()
    {
        var exe = Environment.GetEnvironmentVariable("REAL_EXE");
        Assert.SkipUnless(!string.IsNullOrEmpty(exe) && File.Exists(exe), "Set REAL_EXE to a Windows exe that has an icon.");

        Assert.NotNull(ExeIcon.TryRead(exe!));
    }
}
