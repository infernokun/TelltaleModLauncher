namespace TelltaleModLauncher.Core;

public abstract record LaunchPlan
{
    public sealed record OpenUri(Uri Uri) : LaunchPlan;

    public sealed record StartProcess(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory) : LaunchPlan;

    public sealed record Unavailable(string Reason) : LaunchPlan;
}

public static class GameLauncher
{
    /// <summary>
    /// On Windows the exe runs directly, as before. Elsewhere the game is a Windows build
    /// running through Proton, so it starts through Steam, or through Wine when Steam doesn't
    /// own the install.
    /// </summary>
    public static LaunchPlan Plan(GameVersionSettings game, SteamApp? steam, bool isWindows, Func<string, string?> findOnPath)
    {
        if (!game.ExeExists)
            return new LaunchPlan.Unavailable($"The game's executable isn't at {game.Game_LocationExe} anymore. Set the game's location again.");

        if (isWindows)
            return new LaunchPlan.StartProcess(game.Game_LocationExe, [], game.Game_Location);

        if (steam != null)
            return new LaunchPlan.OpenUri(new Uri($"steam://rungameid/{steam.AppId}"));

        var wine = findOnPath("wine");
        if (wine != null)
            return new LaunchPlan.StartProcess(wine, [game.Game_LocationExe], game.Game_Location);

        return new LaunchPlan.Unavailable(
            "This copy of the game isn't in a Steam library and Wine isn't installed. " +
            "Start the game from the app you installed it with. Your mods will still load.");
    }

    public static string? FindOnPath(string program)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, program);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
