using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.ViewModels;

public sealed partial class GameItemViewModel : ObservableObject
{
    public GameItemViewModel(GameVersionSettings settings, Func<GameItemViewModel, Task> changeLocation, Func<GameItemViewModel, Task> remove)
    {
        Settings = settings;
        Name = GameCatalog.DisplayName(settings.Game_Version);
        IsSupported = GameCatalog.IsSupported(settings.Game_Version);
        Steam = settings.ExeExists ? Core.Steam.FindApp(settings.Game_LocationExe) : null;
        Title = string.IsNullOrWhiteSpace(Steam?.Name) ? Name : Steam.Name;
        Art = LoadArt(Steam);
        ChangeLocationCommand = new AsyncRelayCommand(() => changeLocation(this));
        RemoveCommand = new AsyncRelayCommand(() => remove(this));
    }

    public GameVersionSettings Settings { get; }
    public GameVersion Version => Settings.Game_Version;
    public string Name { get; }
    public string Title { get; }
    public string HeroTitle => Title.ToUpperInvariant();
    public string Tagline => IsSupported ? "SUPPORTED GAME" : "UNTESTED WITH THE LAUNCHER";
    public bool IsSupported { get; }
    public SteamApp? Steam { get; }
    public Bitmap? Art { get; }
    public bool HasArt => Art != null;
    public string Initial => Name[..1];

    public IAsyncRelayCommand ChangeLocationCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }

    [ObservableProperty]
    public partial string Meta { get; set; } = "";

    /// <summary>Second line in the sidebar. A null count means mods can't be read for this game.</summary>
    public void UpdateMeta(int? modCount)
    {
        var status = IsSupported ? "Supported" : "Untested";
        Meta = !Settings.ExeExists ? $"{status} · Not found"
            : modCount is { } n ? $"{status} · {n} {(n == 1 ? "mod" : "mods")}"
            : status;
    }

    private static Bitmap? LoadArt(SteamApp? steam)
    {
        if (steam == null || Core.Steam.FindArtwork(steam) is not { } path)
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, 1600);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
