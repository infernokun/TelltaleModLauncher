namespace TelltaleModLauncher.Core;

/// <summary>
/// File moves and deletes that tolerate brief locks.
/// </summary>
/// <remarks>
/// On Windows, antivirus, the search indexer and sync tools such as OneDrive or Syncthing open
/// a file for a moment right after it is written. Replacing or deleting it during that window
/// fails with "access denied" or a sharing violation even though nothing is wrong, so these
/// helpers retry for a short while before giving up. A file that stays locked, for example an
/// archive the running game has open, still fails with the original exception.
/// </remarks>
public static class FileOps
{
    private const int Attempts = 8;

    public static void Move(string source, string destination, bool overwrite = false) =>
        Retry(() => File.Move(source, destination, overwrite));

    public static void Delete(string path) =>
        Retry(() =>
        {
            if (File.Exists(path))
                File.Delete(path);
        });

    private static void Retry(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception e) when (attempt < Attempts && IsTransient(e))
            {
                Thread.Sleep(attempt * 15); // 15, 30, ... 105 ms: about 0.4 s in total
            }
        }
    }

    // FileNotFound and DirectoryNotFound are IOExceptions too, but waiting won't fix them.
    private static bool IsTransient(Exception e) =>
        e is UnauthorizedAccessException
        || (e is IOException && e is not FileNotFoundException && e is not DirectoryNotFoundException);
}
