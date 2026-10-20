using CommunityToolkit.Mvvm.Input;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.ViewModels;

public sealed class ModItemViewModel
{
    public ModItemViewModel(InstalledMod mod, Func<ModItemViewModel, Task> view, Func<ModItemViewModel, Task> remove)
    {
        Mod = mod;
        ViewCommand = new AsyncRelayCommand(() => view(this));
        RemoveCommand = new AsyncRelayCommand(() => remove(this));
    }

    public InstalledMod Mod { get; }
    public Mod Info => Mod.Info;
    public string Name => Info.ModDisplayName;
    public string Byline => Join(Info.ModAuthor.Length > 0 ? $"by {Info.ModAuthor}" : null, Info.ModVersion.Length > 0 ? $"v{Info.ModVersion}" : null);

    public string Meta
    {
        get
        {
            var count = Info.AllFiles.Count();
            return Join(Byline.Length > 0 ? Byline : null, $"{count} {(count == 1 ? "file" : "files")}");
        }
    }

    public string PriorityText => Mod.Priority?.ToString() ?? "?";

    public string PriorityTip => Mod.Priority is { } p
        ? $"Archive priority {p}. Higher numbers load over lower ones."
        : "Priority unknown. The mod's resource description is encrypted or missing.";

    public string MadeFor => GameCatalog.TryParse(Info.ModCompatibility, out var game)
        ? GameCatalog.DisplayName(game)
        : Info.ModCompatibility.Length > 0 ? Info.ModCompatibility : "Not stated";

    public bool HasMissingFiles => Mod.HasMissingFiles;

    public string MissingText => Mod.MissingFiles.Count == 1
        ? "1 file is missing. Remove the mod and add it again."
        : $"{Mod.MissingFiles.Count} files are missing. Remove the mod and add it again.";

    public IAsyncRelayCommand ViewCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
