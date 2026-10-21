using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.ViewModels;

public sealed partial class ModItemViewModel : ObservableObject
{
    public ModItemViewModel(InstalledMod mod, Func<ModItemViewModel, Task> view, Func<ModItemViewModel, Task> remove,
        Func<ModItemViewModel, int, Task>? move = null, bool canReorder = false)
    {
        Mod = mod;
        CanReorder = canReorder;
        ViewCommand = new AsyncRelayCommand(() => view(this));
        RemoveCommand = new AsyncRelayCommand(() => remove(this));
        MoveUpCommand = new AsyncRelayCommand(() => move?.Invoke(this, -1) ?? Task.CompletedTask, () => CanReorder && move != null);
        MoveDownCommand = new AsyncRelayCommand(() => move?.Invoke(this, 1) ?? Task.CompletedTask, () => CanReorder && move != null);
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

    /// <summary>True when the mod's priority can be rewritten, so it can be dragged to a new place.</summary>
    public bool CanReorder { get; }

    public string PriorityText => Mod.Priority?.ToString() ?? "?";

    public string PriorityTip => (Mod.Priority, CanReorder) switch
    {
        ({ } p, true) => $"Priority {p}. Higher numbers load over lower ones. Drag the mod up or down to change it.",
        ({ } p, false) => $"Priority {p}, taken from the mod's info file. The launcher can't edit this mod's resource description.",
        _ => "Priority unknown. The mod's resource description is missing or can't be read.",
    };

    public string MadeFor => GameCatalog.TryParse(Info.ModCompatibility, out var game)
        ? GameCatalog.DisplayName(game)
        : Info.ModCompatibility.Length > 0 ? Info.ModCompatibility : "Not stated";

    public bool HasMissingFiles => Mod.HasMissingFiles;

    public string MissingText => Mod.MissingFiles.Count == 1
        ? "1 file is missing. Remove the mod and add it again."
        : $"{Mod.MissingFiles.Count} files are missing. Remove the mod and add it again.";

    /// <summary>Set while this card is being dragged.</summary>
    [ObservableProperty]
    public partial bool IsDragging { get; set; }

    /// <summary>Set on the card the dragged mod would land above.</summary>
    [ObservableProperty]
    public partial bool DropAbove { get; set; }

    /// <summary>Set on the card the dragged mod would land below.</summary>
    [ObservableProperty]
    public partial bool DropBelow { get; set; }

    public IAsyncRelayCommand ViewCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand MoveUpCommand { get; }
    public IAsyncRelayCommand MoveDownCommand { get; }

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
