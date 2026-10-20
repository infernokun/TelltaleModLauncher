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
}
