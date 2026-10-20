using Avalonia.Controls;
using Avalonia.Interactivity;
using TelltaleModLauncher.Core;
using TelltaleModLauncher.ViewModels;

namespace TelltaleModLauncher.Views;

public sealed record ModFileItem(string Name, string Detail, string Path, bool Exists);

/// <summary>A mod's info and files, with a preview for the text files it ships.</summary>
public partial class ModDetailsWindow : Window
{
    private const long PreviewLimit = 1024 * 1024;
    private static readonly string[] TextExtensions = [".txt", ".ini", ".cfg", ".json", ".md", ".lua"];

    public ModDetailsWindow()
    {
        InitializeComponent();
    }

    public ModDetailsWindow(ModItemViewModel mod, ModManager manager) : this()
    {
        Title = mod.Name;
        Heading.Text = mod.Name.ToUpperInvariant();
        Byline.Text = mod.Byline;
        Byline.IsVisible = mod.Byline.Length > 0;
        MadeFor.Text = mod.MadeFor;
        Priority.Text = mod.Mod.Priority?.ToString() ?? "Unknown";
        ToolTip.SetTip(Priority, mod.PriorityTip);
        InfoFile.Text = Path.GetFileName(mod.Mod.InfoFilePath);
        ToolTip.SetTip(InfoFile, mod.Mod.InfoFilePath);

        var info = mod.Info;
        Files.ItemsSource = info.ModFiles
            .Concat(info.InstalledFiles ?? [])
            .Concat(info.InstalledGameFiles ?? info.ModFilesGameDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(Item)
            .ToList();
        Files.SelectionChanged += (_, _) => ShowPreview(Files.SelectedItem as ModFileItem);

        ModFileItem Item(string file)
        {
            var path = manager.PathOf(info, file);
            var location = ModManager.IsInGameFolder(info, file) ? "Game folder" : "Mods folder";
            var exists = File.Exists(path);
            return new ModFileItem(file, exists ? location : $"{location} · Missing", path, exists);
        }
    }

    private void ShowPreview(ModFileItem? file)
    {
        string? text = null;
        var message = "Select a text file to read it here.";
        if (file != null)
        {
            if (ReadPreview(file, out var result))
                text = result;
            else
                message = result;
        }

        Preview.Text = text;
        PreviewScroll.IsVisible = text != null;
        PreviewHint.Text = message;
        PreviewHint.IsVisible = text == null;
    }

    /// <summary>Returns true with the file's text, or false with the reason it can't be shown.</summary>
    private static bool ReadPreview(ModFileItem file, out string result)
    {
        if (!file.Exists)
        {
            result = "This file isn't on disk anymore. Remove the mod and add it again.";
            return false;
        }

        if (!TextExtensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase))
        {
            result = "Only text files can be shown here. Archives such as .ttarch2 are binary.";
            return false;
        }

        try
        {
            if (new FileInfo(file.Path).Length > PreviewLimit)
            {
                result = "This file is too large to show here.";
                return false;
            }

            var bytes = File.ReadAllBytes(file.Path);
            if (bytes.Length > 0 && bytes[0] == 0x1B)
            {
                result = "This script is compiled or encrypted, so it can't be shown.";
                return false;
            }

            result = System.Text.Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            result = e.Message;
            return false;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close(true);
}
