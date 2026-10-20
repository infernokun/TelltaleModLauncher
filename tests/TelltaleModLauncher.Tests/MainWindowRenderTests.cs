using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;
using TelltaleModLauncher.Views;

// Bitmap.Save(string) is marked obsolete in Avalonia 12 but still writes a PNG, which is all these tests need.
#pragma warning disable CS0618

[assembly: AvaloniaTestApplication(typeof(TelltaleModLauncher.Tests.TestAppBuilder))]

namespace TelltaleModLauncher.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHarfBuzz()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Renders the real window with the real styles. Set SCREENSHOT_DIR to keep the frames.
/// </summary>
public class MainWindowRenderTests
{
    private static MainWindow Open(AppSettings settings, bool light = false)
    {
        Application.Current!.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
        var window = MainWindow.Create(settings, new FakeShell());
        window.Width = 1180;
        window.Height = 760;
        window.Show();
        return window;
    }

    private static void Save(Window window, string name, int width = 1180)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(width, frame.PixelSize.Width);

        if (Environment.GetEnvironmentVariable("SCREENSHOT_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            frame.Save(Path.Combine(dir, name));
        }
    }

    private static AppSettings WithMods(TestFolder folder, string exe)
    {
        var settings = AppSettings.Load(folder.ConfigPath);
        settings.For(GameVersion.The_Walking_Dead_Definitive_Series).SetExe(exe);
        settings.Save();

        var manager = new ModManager(Path.Combine(Path.GetDirectoryName(exe)!, "Archives"), Path.GetDirectoryName(exe)!);
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Graphic Black Disabler for S4", "1.0.0", author: "changemymindpls1",
            files: ["GraphicBlackDisabler.ttarch2", "_resdesc_50_GraphicBlackDisabler.lua"])));
        manager.Install(ModManager.ReadPackage(folder.CreateModZip("Load Any Level", "4.0.0", author: "droyti", infoName: "LoadAnyLevel.json",
            files: ["LoadAnyLevel.ttarch2", "_resdesc_50_LoadAnyLevel.lua", "readme.txt"])));
        File.WriteAllText(Path.Combine(manager.ModsDirectory, "_resdesc_50_GraphicBlackDisabler.lua"), "set.priority = 1100\n");
        File.WriteAllText(Path.Combine(manager.ModsDirectory, "_resdesc_50_LoadAnyLevel.lua"), "set.priority = 1000\n");
        return settings;
    }

    [AvaloniaFact]
    public void Renders_the_library_with_mods()
    {
        using var folder = new TestFolder();
        var window = Open(WithMods(folder, folder.CreateGame()));

        var vm = (MainWindowViewModel)window.DataContext!;
        Assert.Equal(["Graphic Black Disabler for S4", "Load Any Level"], vm.Mods.Select(m => m.Name));
        Assert.Equal("Play with 2 mods", vm.PlayLabel);
        Save(window, "library-dark.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Renders_the_light_theme()
    {
        using var folder = new TestFolder();
        var window = Open(WithMods(folder, folder.CreateGame()), light: true);
        Save(window, "library-light.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Renders_the_first_run_state()
    {
        using var folder = new TestFolder();
        var window = Open(AppSettings.Load(folder.ConfigPath));
        Save(window, "first-run.png");
        window.Close();
    }

    [AvaloniaFact]
    public void Renders_the_dialogs()
    {
        using var folder = new TestFolder();
        var exe = folder.CreateGame();
        var settings = WithMods(folder, exe);
        var manager = new ModManager(Path.Combine(Path.GetDirectoryName(exe)!, "Archives"), Path.GetDirectoryName(exe)!);
        File.WriteAllText(Path.Combine(manager.ModsDirectory, "readme.txt"), "Load Any Level\n\nPress F1 in game to open the level list.");
        var mod = new ModItemViewModel(manager.Scan().Single(m => m.Info.ModDisplayName == "Load Any Level"), _ => Task.CompletedTask, _ => Task.CompletedTask);

        var details = new ModDetailsWindow(mod, manager);
        details.Show();
        details.FindControl<ListBox>("Files")!.SelectedIndex = 2;
        Assert.Contains("F1", details.FindControl<SelectableTextBlock>("Preview")!.Text);
        Save(details, "dialog-mod-details.png", 860);

        var setup = new GameSetupWindow(new GameSetupRequest(null, exe, []));
        setup.Show();
        Assert.True(setup.FindControl<Button>("SaveButton")!.IsEnabled);
        Save(setup, "dialog-add-game.png", 580);

        var message = new MessageWindow("Remove mod", "Remove 'Load Any Level'? Its files are deleted from the game folder.", "Remove", "Cancel", true);
        message.Show();
        Save(message, "dialog-confirm.png", 480);

        var settingsWindow = new SettingsWindow(settings);
        settingsWindow.Show();
        Save(settingsWindow, "dialog-settings.png", 500);
    }

    [AvaloniaFact]
    public void Renders_steam_artwork_behind_the_title()
    {
        using var folder = new TestFolder();
        var exe = SteamTests.CreateSteamGame(folder);
        var art = Path.Combine(folder.Root, "Steam", "appcache", "librarycache", "1449690", "library_hero.png");
        Directory.CreateDirectory(Path.GetDirectoryName(art)!);
        using (var bitmap = new RenderTargetBitmap(new PixelSize(1920, 620)))
        {
            using (var context = bitmap.CreateDrawingContext())
            {
                context.FillRectangle(new SolidColorBrush(Color.Parse("#4A5A3A")), new Rect(0, 0, 1920, 620));
                context.FillRectangle(new SolidColorBrush(Color.Parse("#8C6A3C")), new Rect(0, 300, 1920, 320));
            }
            bitmap.Save(art);
        }

        var window = Open(WithMods(folder, exe));

        var vm = (MainWindowViewModel)window.DataContext!;
        Assert.True(vm.SelectedGame?.HasArt);
        Assert.Equal("THE WALKING DEAD: THE TELLTALE DEFINITIVE SERIES", vm.SelectedGame?.HeroTitle);
        Save(window, "library-art.png");
        window.Close();
    }
}
