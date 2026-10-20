using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TelltaleModLauncher.Core;

/// <summary>Installs, lists and removes mods for one game.</summary>
public sealed partial class ModManager(string modsDirectory, string gameDirectory)
{
    /// <summary>Suffix for a game file that a mod replaced. It is put back when the mod is removed.</summary>
    public const string BackupSuffix = ".tmlbak";

    public string ModsDirectory { get; } = Path.GetFullPath(modsDirectory);
    public string GameDirectory { get; } = Path.GetFullPath(gameDirectory);

    /// <summary>Reads every mod info json in the mods folder, highest priority first.</summary>
    public IReadOnlyList<InstalledMod> Scan()
    {
        var mods = new List<InstalledMod>();
        try
        {
            foreach (var jsonPath in Directory.EnumerateFiles(ModsDirectory, "*.json"))
            {
                Mod? info;
                try
                {
                    info = Mod.TryParse(File.ReadAllText(jsonPath));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                if (info == null)
                    continue;

                var missing = ListedFiles(info).Where(f => !File.Exists(PathOf(info, f))).ToList();
                mods.Add(new InstalledMod(info, jsonPath, missing, info.ModResourcePriority ?? ReadResdescPriority(info)));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The folder vanished or can't be listed; show what was read.
        }

        return mods
            .OrderByDescending(m => m.Priority.HasValue)
            .ThenByDescending(m => m.Priority)
            .ThenBy(m => m.Info.ModDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Opens a mod zip and reads its info json without extracting anything.</summary>
    public static ModPackage ReadPackage(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !IsJunk(e.FullName)))
            {
                if (ReadInfo(entry) is { } info)
                    return new ModPackage(Path.GetFullPath(zipPath), info, entry.FullName);
            }
        }
        catch (InvalidDataException e)
        {
            throw new ModPackageException("This file isn't a zip archive the launcher can open.", e);
        }
        catch (NotSupportedException e)
        {
            throw Unsupported(e);
        }

        throw new ModPackageException(
            "This zip has no mod info file, so it isn't packaged for the launcher. " +
            "Make sure you downloaded the mod's release zip and not its source code.");
    }

    public InstallPlan Plan(ModPackage package, GameVersion game, IReadOnlyList<InstalledMod> installed)
    {
        GameVersion? modGame = GameCatalog.TryParse(package.Info.ModCompatibility, out var parsed) ? parsed : null;

        var existing = installed.FirstOrDefault(m =>
            string.Equals(m.Info.ModDisplayName, package.Info.ModDisplayName, StringComparison.OrdinalIgnoreCase));

        var newFiles = package.Info.AllFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<InstalledMod>();
        var conflictingFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in installed.Where(m => m != existing))
        {
            var shared = mod.Info.AllFiles.Where(newFiles.Contains).ToList();
            if (shared.Count == 0)
                continue;
            conflicts.Add(mod);
            conflictingFiles.UnionWith(shared);
        }

        return new InstallPlan(modGame == game, modGame, existing, conflicts, conflictingFiles.ToList());
    }

    /// <summary>
    /// Extracts a package. Each zip entry is matched to a path the info json lists, and goes to
    /// that path under the game folder (ModFilesGameDirectory) or the mods folder (ModFiles). A
    /// game file it replaces is kept as a backup. Files the json doesn't list go flat into the
    /// mods folder, as earlier releases did. Every file is extracted to a staging folder first,
    /// so a bad zip leaves the installed mods alone.
    /// </summary>
    public void Install(ModPackage package, IEnumerable<InstalledMod>? replacing = null)
    {
        var info = package.Info;

        using var archive = ZipFile.OpenRead(package.ZipPath);
        var infoEntry = archive.Entries.First(e => e.FullName == package.InfoEntryName);

        var files = new List<(ZipArchiveEntry Entry, string Target, string Name, bool ToGame)>();
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var entryPath = Mod.RelativePath(entry.FullName);
            var name = Mod.FileName(entryPath);
            if (entry == infoEntry || name.Length == 0 || !Path.HasExtension(name) || IsJunk(entryPath))
                continue;
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && ReadInfo(entry) != null)
                continue; // another mod's info file, such as a variant in the same zip

            // The json's spelling wins so the files can be found again on a case-sensitive disk.
            var listed = MatchListed(info.ModFilesGameDirectory, entryPath, name);
            var toGame = listed != null;
            listed ??= MatchListed(info.ModFiles, entryPath, name);
            var relative = listed ?? name;

            var target = SafeCombine(toGame ? GameDirectory : ModsDirectory, relative);
            if (targets.Add(target))
                files.Add((entry, target, relative, toGame));
        }

        var staging = Path.Combine(ModsDirectory, $".tml-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            for (var i = 0; i < files.Count; i++)
                files[i].Entry.ExtractToFile(Path.Combine(staging, i.ToString()));

            foreach (var mod in replacing ?? [])
                Remove(mod);

            for (var i = 0; i < files.Count; i++)
            {
                var (_, target, _, toGame) = files[i];
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (toGame && File.Exists(target) && !File.Exists(target + BackupSuffix))
                    File.Move(target, target + BackupSuffix);
                File.Move(Path.Combine(staging, i.ToString()), target, overwrite: true);
            }

            WriteInfo(infoEntry, files.Where(f => !f.ToGame).Select(f => f.Name), files.Where(f => f.ToGame).Select(f => f.Name));
        }
        catch (NotSupportedException e)
        {
            throw Unsupported(e);
        }
        finally
        {
            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    public void Remove(InstalledMod mod)
    {
        var info = mod.Info;
        var modsFolderFiles = info.ModFiles.Concat(info.InstalledFiles ?? []);
        if (info.InstalledGameFiles == null)
            modsFolderFiles = modsFolderFiles.Concat(info.ModFilesGameDirectory); // earlier releases put these in the mods folder

        // A json could list "../" paths; those are skipped rather than deleted.
        foreach (var file in modsFolderFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Inside(ModsDirectory, file) is { } path)
                DeleteIfExists(path);
        }

        foreach (var file in info.InstalledGameFiles ?? [])
        {
            if (Inside(GameDirectory, file) is not { } path)
                continue;
            DeleteIfExists(path);
            if (File.Exists(path + BackupSuffix))
                File.Move(path + BackupSuffix, path);
        }

        DeleteIfExists(mod.InfoFilePath);
    }

    /// <summary>True when the launcher put this file next to the game exe.</summary>
    public static bool IsInGameFolder(Mod info, string file) =>
        info.InstalledGameFiles?.Contains(file, StringComparer.OrdinalIgnoreCase) == true;

    /// <summary>Full path of a file a mod lists, in whichever folder it was installed to.</summary>
    public string PathOf(Mod info, string file) =>
        Path.Combine(IsInGameFolder(info, file) ? GameDirectory : ModsDirectory, file);

    /// <summary>The files that should be on disk for an installed mod.</summary>
    private static IEnumerable<string> ListedFiles(Mod info) =>
        info.ModFiles.Concat(info.InstalledGameFiles ?? info.ModFilesGameDirectory).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Archive priority from the mod's resource description (_resdesc_*.lua). Only plain text
    /// files can be read; encrypted ones start with an escape byte and are skipped.
    /// </summary>
    internal int? ReadResdescPriority(Mod info)
    {
        foreach (var file in info.ModFiles)
        {
            var name = Mod.FileName(file);
            if (!name.StartsWith("_resdesc_", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                continue;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(Path.Combine(ModsDirectory, file));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            if (bytes.Length == 0 || bytes[0] == 0x1B)
                continue;

            var match = PriorityPattern().Match(Encoding.UTF8.GetString(bytes));
            if (match.Success && int.TryParse(match.Groups[1].Value, out var priority))
                return priority;
        }
        return null;
    }

    private static Mod? ReadInfo(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return Mod.TryParse(reader.ReadToEnd());
    }

    /// <summary>Saves the mod's info json with a record of where each file went.</summary>
    private void WriteInfo(ZipArchiveEntry infoEntry, IEnumerable<string> modsFolderFiles, IEnumerable<string> gameFiles)
    {
        string text;
        using (var reader = new StreamReader(infoEntry.Open(), Encoding.UTF8))
            text = reader.ReadToEnd();

        var node = (JsonObject)JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        })!;
        node[nameof(Mod.InstalledFiles)] = new JsonArray(modsFolderFiles.Select(f => (JsonNode)f).ToArray());
        node[nameof(Mod.InstalledGameFiles)] = new JsonArray(gameFiles.Select(f => (JsonNode)f).ToArray());

        File.WriteAllText(UniqueInfoPath(Mod.FileName(infoEntry.FullName)), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Two mods may ship an info json with the same name, so never overwrite another mod's.</summary>
    private string UniqueInfoPath(string fileName)
    {
        var path = SafeCombine(ModsDirectory, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var n = 2; File.Exists(path); n++)
            path = SafeCombine(ModsDirectory, $"{stem}_{n}.json");
        return path;
    }

    /// <summary>
    /// The path the json lists for a zip entry: one the entry's path ends with, or failing
    /// that the only listed path with the same file name. Null when the json doesn't list it.
    /// </summary>
    private static string? MatchListed(IReadOnlyList<string> listed, string entryPath, string name)
    {
        var exact = listed.FirstOrDefault(l =>
            entryPath.Equals(l, StringComparison.OrdinalIgnoreCase) || entryPath.EndsWith("/" + l, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;

        var sameName = listed.Where(l => Mod.FileName(l).Equals(name, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return sameName.Count == 1 ? sameName[0] : null;
    }

    /// <summary>Files that archivers and file browsers add, such as the __MACOSX folder from macOS zips.</summary>
    internal static bool IsJunk(string path)
    {
        var relative = Mod.RelativePath(path);
        var name = Mod.FileName(relative);
        return relative.Split('/').Contains("__MACOSX", StringComparer.OrdinalIgnoreCase)
            || name.StartsWith("._", StringComparison.Ordinal)
            || name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)
            || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeCombine(string directory, string relativePath) =>
        Inside(directory, relativePath)
        ?? throw new ModPackageException($"The mod lists a file, '{relativePath}', that would land outside the game folder.");

    /// <summary>The full path of a relative path under a folder, or null when it points outside it.</summary>
    private static string? Inside(string directory, string relativePath)
    {
        var root = Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        return path.StartsWith(root, StringComparison.Ordinal) ? path : null;
    }

    private static ModPackageException Unsupported(NotSupportedException e) =>
        new("This zip uses a compression method the launcher can't read. Zip the files again with standard (Deflate) compression.", e);

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    [GeneratedRegex(@"set\.priority\s*=\s*(-?\d+)")]
    private static partial Regex PriorityPattern();
}
