using System.Text.RegularExpressions;

namespace TelltaleModLauncher.Core;

public sealed record SteamApp(string AppId, string Name, string LibraryPath);

/// <summary>Finds Steam's record of an installed game from the path of its executable.</summary>
public static partial class Steam
{
    /// <summary>
    /// Walks up from the exe to steamapps/common/&lt;installdir&gt; and matches that folder
    /// against the library's appmanifest files.
    /// </summary>
    public static SteamApp? FindApp(string exePath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? "");
        while (dir?.Parent?.Parent is { } steamapps)
        {
            if (dir.Parent.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                && steamapps.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                return FindInLibrary(steamapps, dir.Name);
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static SteamApp? FindInLibrary(DirectoryInfo steamapps, string installDir)
    {
        IEnumerable<FileInfo> manifests;
        try
        {
            manifests = steamapps.EnumerateFiles("appmanifest_*.acf").ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (var manifest in manifests)
        {
            Dictionary<string, string> values;
            try
            {
                values = ReadAcf(File.ReadAllText(manifest.FullName));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            if (values.TryGetValue("installdir", out var dir)
                && dir.Equals(installDir, StringComparison.OrdinalIgnoreCase)
                && values.TryGetValue("appid", out var appId))
            {
                return new SteamApp(appId, values.GetValueOrDefault("name", installDir), steamapps.Parent!.FullName);
            }
        }
        return null;
    }

    /// <summary>The first value for each key in a Valve KeyValues file.</summary>
    internal static Dictionary<string, string> ReadAcf(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AcfPair().Matches(text))
            values.TryAdd(match.Groups[1].Value, match.Groups[2].Value);
        return values;
    }

    /// <summary>
    /// Steam's cached library artwork for the app. The cache moved from flat files to one
    /// folder per app in 2024, so both layouts are checked.
    /// </summary>
    public static string? FindArtwork(SteamApp app, IEnumerable<string>? steamRoots = null)
    {
        string[] names = ["library_hero.jpg", "library_hero.png", "header.jpg", "library_header.jpg"];
        var roots = new[] { app.LibraryPath }.Concat(steamRoots ?? KnownSteamRoots()).Distinct();

        foreach (var root in roots)
        {
            var cache = Path.Combine(root, "appcache", "librarycache");
            if (!Directory.Exists(cache))
                continue;

            foreach (var name in names)
            {
                var flat = Path.Combine(cache, $"{app.AppId}_{name}");
                if (File.Exists(flat))
                    return flat;
            }

            var appFolder = Path.Combine(cache, app.AppId);
            if (!Directory.Exists(appFolder))
                continue;

            try
            {
                foreach (var name in names)
                {
                    var found = Directory.EnumerateFiles(appFolder, name, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null)
                        return found;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return null;
    }

    public static IEnumerable<string> KnownSteamRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
            yield break;
        }

        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
    }

    [GeneratedRegex("\"([^\"]+)\"\\s+\"([^\"]*)\"")]
    private static partial Regex AcfPair();
}
