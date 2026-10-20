using System.Text.Json;
using System.Text.Json.Nodes;

namespace TelltaleModLauncher.Core;

public sealed class AppSettingsFile
{
    /// <summary>The game selected when the launcher opens. The launcher keeps it on the last game picked.</summary>
    public GameVersion Default_Game_Version { get; set; }
    public bool UI_LightMode { get; set; }
    public bool UI_WindowDefocusing { get; set; } = true;
}

public sealed class GameVersionSettings
{
    public const string Undefined = "Undefined";

    private static readonly string[] ModFolderNames = ["Archives", "Packs", "Pack"];

    public GameVersion Game_Version { get; set; }
    public string Game_Location { get; set; } = Undefined;
    public string Game_LocationExe { get; set; } = Undefined;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Game_LocationExe) && Game_LocationExe != Undefined;

    public bool ExeExists => IsConfigured && File.Exists(Game_LocationExe);

    public void SetExe(string exePath)
    {
        Game_LocationExe = Path.GetFullPath(exePath);
        Game_Location = Path.GetDirectoryName(Game_LocationExe) ?? Undefined;
    }

    public void Clear()
    {
        Game_LocationExe = Undefined;
        Game_Location = Undefined;
    }

    /// <summary>
    /// The folder holding the game's archives: Archives, Packs or Pack. Matched without case so
    /// a Windows install copied to Linux still resolves.
    /// </summary>
    public string? FindModsDirectory() => FindModsDirectory(Game_Location);

    public static string? FindModsDirectory(string? gameLocation)
    {
        if (string.IsNullOrWhiteSpace(gameLocation) || !Directory.Exists(gameLocation))
            return null;

        var folders = Directory.GetDirectories(gameLocation);
        foreach (var name in ModFolderNames)
        {
            var match = folders.FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        return null;
    }
}

/// <summary>
/// The launcher config. The file layout matches what earlier releases wrote: a json array
/// holding the app settings object and then the list of per game settings.
/// </summary>
public sealed class AppSettings
{
    public const string FileName = "TelltaleModLauncher_Config.json";
    public const string HelpLink = "https://github.com/Telltale-Modding-Group/TelltaleModLauncher/wiki/%5BHelp%5D";
    public const string SetupHelpLink = "https://github.com/Telltale-Modding-Group/TelltaleModLauncher/wiki/%5BHelp%5D-Application-Setup";

    private AppSettings(string filePath, AppSettingsFile app, List<GameVersionSettings> games)
    {
        FilePath = filePath;
        App = app;
        Games = games;
    }

    public string FilePath { get; }
    public AppSettingsFile App { get; }
    public IReadOnlyList<GameVersionSettings> Games { get; }

    /// <summary>Set when the config file could not be read and was set aside.</summary>
    public string? LoadWarning { get; private set; }

    public IEnumerable<GameVersionSettings> ConfiguredGames => Games.Where(g => g.IsConfigured);

    /// <summary>
    /// Windows keeps the old Documents location so existing setups carry over. Other systems
    /// use the per user config folder (~/.config on Linux).
    /// </summary>
    public static string DefaultFilePath
    {
        get
        {
            var root = Environment.GetFolderPath(OperatingSystem.IsWindows()
                ? Environment.SpecialFolder.MyDocuments
                : Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "TelltaleModLauncher", FileName);
        }
    }

    public GameVersionSettings For(GameVersion version) => Games.First(g => g.Game_Version == version);

    public static AppSettings Load(string filePath)
    {
        var app = new AppSettingsFile();
        var games = new List<GameVersionSettings>();
        string? warning = null;

        if (File.Exists(filePath))
        {
            try
            {
                Parse(File.ReadAllText(filePath), app, games);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or InvalidCastException or ArgumentException)
            {
                var backup = filePath + ".bak";
                File.Copy(filePath, backup, overwrite: true);
                app = new AppSettingsFile();
                games.Clear();
                warning = $"The settings file couldn't be read, so the launcher started fresh. The old file was saved as {backup}.";
            }
        }

        foreach (var version in GameCatalog.All)
        {
            if (games.All(g => g.Game_Version != version))
                games.Add(new GameVersionSettings { Game_Version = version });
        }
        games.Sort((a, b) => a.Game_Version.CompareTo(b.Game_Version));

        var settings = new AppSettings(filePath, app, games) { LoadWarning = warning };
        if (!File.Exists(filePath) || warning != null)
            settings.Save();
        return settings;
    }

    private static void Parse(string json, AppSettingsFile app, List<GameVersionSettings> games)
    {
        var root = JsonNode.Parse(json)?.AsArray() ?? throw new FormatException("Empty settings file.");
        if (root.Count == 0)
            throw new FormatException("Empty settings file.");

        if (root[0] is JsonObject appObject)
        {
            if (appObject[nameof(AppSettingsFile.Default_Game_Version)] is JsonValue version)
                app.Default_Game_Version = (GameVersion)version.GetValue<int>();
            if (appObject[nameof(AppSettingsFile.UI_LightMode)] is JsonValue light)
                app.UI_LightMode = light.GetValue<bool>();
            if (appObject[nameof(AppSettingsFile.UI_WindowDefocusing)] is JsonValue defocus)
                app.UI_WindowDefocusing = defocus.GetValue<bool>();
        }

        if (root.Count < 2 || root[1] is not JsonArray gameArray)
            return;

        foreach (var node in gameArray.OfType<JsonObject>())
        {
            if (node[nameof(GameVersionSettings.Game_Version)] is not JsonValue versionValue)
                continue;

            var version = (GameVersion)versionValue.GetValue<int>();
            if (!Enum.IsDefined(version) || games.Any(g => g.Game_Version == version))
                continue;

            games.Add(new GameVersionSettings
            {
                Game_Version = version,
                Game_Location = node[nameof(GameVersionSettings.Game_Location)]?.GetValue<string>() ?? GameVersionSettings.Undefined,
                Game_LocationExe = node[nameof(GameVersionSettings.Game_LocationExe)]?.GetValue<string>() ?? GameVersionSettings.Undefined,
            });
        }
    }

    public void Save()
    {
        var root = new JsonArray
        {
            new JsonObject
            {
                [nameof(AppSettingsFile.Default_Game_Version)] = (int)App.Default_Game_Version,
                [nameof(AppSettingsFile.UI_LightMode)] = App.UI_LightMode,
                [nameof(AppSettingsFile.UI_WindowDefocusing)] = App.UI_WindowDefocusing,
            },
            new JsonArray(Games.Select(g => (JsonNode)new JsonObject
            {
                [nameof(GameVersionSettings.Game_Version)] = (int)g.Game_Version,
                [nameof(GameVersionSettings.Game_Location)] = g.Game_Location,
                [nameof(GameVersionSettings.Game_LocationExe)] = g.Game_LocationExe,
            }).ToArray()),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, overwrite: true);
    }
}
