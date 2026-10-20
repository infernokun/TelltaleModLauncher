using System.Text.Json;
using System.Text.Json.Nodes;

namespace TelltaleModLauncher.Core;

/// <summary>The contents of a mod's info json, the file that ships inside every launcher mod zip.</summary>
public sealed class Mod
{
    public string ModDisplayName { get; set; } = "";
    public string ModVersion { get; set; } = "";
    public string ModAuthor { get; set; } = "";
    public string ModCompatibility { get; set; } = "";

    /// <summary>
    /// Files that go in the game's mods folder (Archives, Packs or Pack). Paths are relative to
    /// that folder, so games that split episodes into subfolders can list "Episode101/x.ttarch2".
    /// </summary>
    public List<string> ModFiles { get; set; } = [];

    /// <summary>Files that go in the folder of the game executable, such as .ini configs. Relative to that folder.</summary>
    public List<string> ModFilesGameDirectory { get; set; } = [];

    public int? ModResourcePriority { get; set; }

    /// <summary>
    /// Written by the launcher on install: the files it put in the mods folder and next to the
    /// exe. Null for mods installed by earlier releases.
    /// </summary>
    public List<string>? InstalledFiles { get; set; }
    public List<string>? InstalledGameFiles { get; set; }

    public IEnumerable<string> AllFiles => ModFiles.Concat(ModFilesGameDirectory);

    /// <summary>
    /// Reads an info json. Returns null when the json is not a mod info file, so other json
    /// files in a zip or in the mods folder are skipped.
    /// </summary>
    public static Mod? TryParse(string json)
    {
        // JsonNode reads lazily, so a bad document (duplicate keys, say) throws on first access.
        try
        {
            var obj = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }) as JsonObject;

            var name = Text(obj?[nameof(ModDisplayName)]);
            if (obj == null || string.IsNullOrWhiteSpace(name))
                return null;

            return new Mod
            {
                ModDisplayName = name.Trim(),
                ModVersion = Text(obj[nameof(ModVersion)]),
                ModAuthor = Text(obj[nameof(ModAuthor)]),
                ModCompatibility = Text(obj[nameof(ModCompatibility)]),
                ModFiles = Files(obj[nameof(ModFiles)]),
                ModFilesGameDirectory = Files(obj[nameof(ModFilesGameDirectory)]),
                ModResourcePriority = obj[nameof(ModResourcePriority)] is JsonValue p && p.TryGetValue(out int priority) ? priority : null,
                InstalledFiles = obj[nameof(InstalledFiles)] is JsonArray ? Files(obj[nameof(InstalledFiles)]) : null,
                InstalledGameFiles = obj[nameof(InstalledGameFiles)] is JsonArray ? Files(obj[nameof(InstalledGameFiles)]) : null,
            };
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The file name part of a path from a zip or a mod json, with either slash.</summary>
    public static string FileName(string path) => Path.GetFileName(path.Replace('\\', '/'));

    /// <summary>A path from a zip or a mod json with forward slashes and no leading slash or "./".</summary>
    public static string RelativePath(string path)
    {
        var relative = path.Replace('\\', '/').Trim();
        while (relative.StartsWith("./", StringComparison.Ordinal))
            relative = relative[2..];
        return relative.TrimStart('/');
    }

    private static string Text(JsonNode? node) => node is JsonValue value ? value.ToString().Trim() : "";

    private static List<string> Files(JsonNode? node) =>
        node is JsonArray array
            ? array.Select(Text).Select(RelativePath).Where(f => f.Length > 0 && !f.EndsWith('/')).ToList()
            : [];
}

/// <summary>A mod found in a game's mods folder.</summary>
public sealed record InstalledMod(Mod Info, string InfoFilePath, IReadOnlyList<string> MissingFiles, int? Priority)
{
    public bool HasMissingFiles => MissingFiles.Count > 0;
}

/// <summary>A mod zip that has been opened and has a valid info json.</summary>
public sealed record ModPackage(string ZipPath, Mod Info, string InfoEntryName);

/// <summary>What installing a package would do to the mods already installed.</summary>
public sealed record InstallPlan(
    bool IsCompatible,
    GameVersion? ModGame,
    InstalledMod? Existing,
    IReadOnlyList<InstalledMod> Conflicts,
    IReadOnlyList<string> ConflictingFiles);

/// <summary>A problem with a mod zip, with a message meant for the user.</summary>
public sealed class ModPackageException(string message, Exception? inner = null) : Exception(message, inner);
