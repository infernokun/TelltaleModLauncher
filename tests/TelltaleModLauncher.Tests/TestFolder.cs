using System.IO.Compression;
using System.Text.Json;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

/// <summary>A throwaway folder holding fake game installs and mod zips.</summary>
public sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "tml-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string ConfigPath => Path.Combine(Root, "config", AppSettings.FileName);

    /// <summary>Creates a game folder with an exe and an Archives folder. Returns the exe path.</summary>
    public string CreateGame(string relativeDir = "Game", string exeName = "WDC.exe", string modsFolder = "Archives")
    {
        var dir = Path.Combine(Root, relativeDir);
        Directory.CreateDirectory(Path.Combine(dir, modsFolder));
        var exe = Path.Combine(dir, exeName);
        File.WriteAllText(exe, "");
        return exe;
    }

    public string CreateZip(string name, Dictionary<string, string> entries)
    {
        var path = Path.Combine(Root, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }
        return path;
    }

    /// <summary>A launcher mod zip: an info json plus one entry per listed file.</summary>
    public string CreateModZip(
        string name,
        string version = "1.0.0",
        string game = "The_Walking_Dead_Definitive_Series",
        string[]? files = null,
        string[]? gameFiles = null,
        string infoName = "modinfo.json",
        string folder = "",
        string author = "tester")
    {
        files ??= [$"{name.Replace(" ", "")}.ttarch2"];
        gameFiles ??= [];
        var info = JsonSerializer.Serialize(new
        {
            ModDisplayName = name,
            ModVersion = version,
            ModAuthor = author,
            ModCompatibility = game,
            ModFiles = files,
            ModFilesGameDirectory = gameFiles,
        });

        var entries = new Dictionary<string, string> { [folder + infoName] = info };
        foreach (var file in files.Concat(gameFiles))
            entries[folder + file] = $"contents of {file}";
        return CreateZip($"{name.Replace(" ", "_")}_{version}.zip", entries);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
