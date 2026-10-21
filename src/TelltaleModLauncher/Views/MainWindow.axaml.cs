using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;

namespace TelltaleModLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => SetDragOver(false));
        AddHandler(DragDrop.DropEvent, OnDrop);

        ModList.PointerPressed += OnModPointerPressed;
        ModList.PointerMoved += OnModPointerMoved;
        ModList.PointerReleased += OnModPointerReleased;
        ModList.PointerCaptureLost += (_, _) => EndReorder();

        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                await vm.ShowStartupWarningAsync();
        };

        // Stands in for a file watcher: mods changed by hand show up when the window is focused again.
        Activated += (_, _) => (DataContext as MainWindowViewModel)?.RefreshIfIdle();
    }

    public static MainWindow Create(AppSettings settings, IShell? shell = null)
    {
        var window = new MainWindow();
        var windowShell = shell == null ? new WindowShell(window, settings) : null;
        var vm = new MainWindowViewModel(settings, shell ?? windowShell!);
        if (windowShell != null)
            windowShell.Dimmed = dimmed => vm.IsDimmed = dimmed;
        window.DataContext = vm;
        return window;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private static string? DroppedZip(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?
            .Select(f => f.TryGetLocalPath())
            .FirstOrDefault(p => p != null && p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accept = ViewModel?.CanManageMods == true && DroppedZip(e) != null;
        e.DragEffects = accept ? DragDropEffects.Copy : DragDropEffects.None;
        SetDragOver(accept);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragOver(false);
        if (ViewModel is { } vm && DroppedZip(e) is { } zip)
            await vm.InstallZipAsync(zip);
    }

    private void SetDragOver(bool value)
    {
        if (ViewModel is { } vm)
            vm.IsDragOver = value;
    }

    // Dragging a mod card to change the load order. This is plain pointer tracking inside the
    // list rather than DragDrop, so it can never be mistaken for a zip dropped on the window.
    // Buttons on a card handle their own presses, so those never start a drag.

    private const double ReorderThreshold = 6;
    private ModItemViewModel? pressedMod;
    private Point pressPoint;
    private bool reordering;
    private (ModItemViewModel Target, bool Below)? dropTarget;

    private void OnModPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ModList).Properties.IsLeftButtonPressed)
            return;
        if ((e.Source as StyledElement)?.DataContext is ModItemViewModel { CanReorder: true } mod)
        {
            pressedMod = mod;
            pressPoint = e.GetPosition(ModList);
        }
    }

    private void OnModPointerMoved(object? sender, PointerEventArgs e)
    {
        if (pressedMod is not { } mod)
            return;
        var position = e.GetPosition(ModList);
        if (!reordering)
        {
            if (Math.Abs(position.Y - pressPoint.Y) < ReorderThreshold && Math.Abs(position.X - pressPoint.X) < ReorderThreshold)
                return;
            reordering = true;
            mod.IsDragging = true;
            e.Pointer.Capture(ModList);
        }
        ShowDropTarget(FindDropTarget(position.Y));
    }

    private async void OnModPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var mod = pressedMod;
        var target = dropTarget;
        var wasReordering = reordering;
        e.Pointer.Capture(null);
        EndReorder();

        if (!wasReordering || mod == null || target is not { } t || ViewModel is not { } vm)
            return;
        var from = vm.Mods.IndexOf(mod);
        var to = vm.Mods.IndexOf(t.Target) + (t.Below ? 1 : 0);
        if (to > from)
            to--;
        await vm.MoveModAsync(mod, to);
    }

    /// <summary>The card under the pointer, and whether the drop goes below it.</summary>
    private (ModItemViewModel Target, bool Below)? FindDropTarget(double y)
    {
        if (ViewModel is not { } vm || vm.Mods.Count == 0)
            return null;
        for (var i = 0; i < vm.Mods.Count; i++)
        {
            if (ModList.ContainerFromIndex(i) is not { } container || container.TranslatePoint(default, ModList) is not { } top)
                continue;
            var height = container.Bounds.Height;
            if (y < top.Y + height)
                return (vm.Mods[i], y > top.Y + height / 2);
        }
        return (vm.Mods[^1], true);
    }

    private void ShowDropTarget((ModItemViewModel Target, bool Below)? target)
    {
        ClearDropLines();
        dropTarget = null;
        if (target is not { } t || ViewModel is not { } vm || pressedMod is not { } mod)
            return;

        // Dropping onto itself, or right next to where it already is, changes nothing.
        var from = vm.Mods.IndexOf(mod);
        var at = vm.Mods.IndexOf(t.Target);
        if (t.Target == mod || (at == from - 1 && t.Below) || (at == from + 1 && !t.Below))
            return;

        dropTarget = t;
        if (t.Below)
            t.Target.DropBelow = true;
        else
            t.Target.DropAbove = true;
    }

    private void ClearDropLines()
    {
        foreach (var mod in ViewModel?.Mods ?? [])
            mod.DropAbove = mod.DropBelow = false;
    }

    private void EndReorder()
    {
        if (pressedMod != null)
            pressedMod.IsDragging = false;
        ClearDropLines();
        pressedMod = null;
        dropTarget = null;
        reordering = false;
    }
}
