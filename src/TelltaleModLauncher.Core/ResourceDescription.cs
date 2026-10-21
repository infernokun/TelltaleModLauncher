using System.Text;
using System.Text.RegularExpressions;

namespace TelltaleModLauncher.Core;

/// <summary>
/// Reads and edits the priority in a <c>_resdesc_*.lua</c> file. The game reads the priority
/// from this file, so it decides which mod's files win when two mods replace the same file.
/// The file is either plain Lua or encrypted (it then starts with ESC "LEo"). An edited file
/// keeps its encryption and every byte other than the number.
/// </summary>
public static partial class ResourceDescription
{
    private static readonly byte[] EncryptedHeader = "\u001bLEo"u8.ToArray();

    // The Blowfish key of The Walking Dead: The Telltale Definitive Series, from ttarchext.
    private static readonly byte[] DefinitiveSeriesKey =
    [
        0x96, 0xCA, 0x99, 0x9F, 0x8D, 0xDA, 0x9A, 0x87, 0xD7, 0xCD, 0xD9, 0xBB, 0x93, 0xD1,
        0xBE, 0xC0, 0xD7, 0x91, 0x71, 0xDC, 0x9E, 0xD9, 0x8D, 0xD0, 0xD1, 0x8C, 0xD8, 0xC3,
        0xA0, 0xB0, 0xC6, 0x95, 0xC3, 0x9C, 0x93, 0xBB, 0xCC, 0xCC, 0xA7, 0xD3, 0xB9, 0xD9,
        0xD9, 0xD0, 0x8E, 0x93, 0xBE, 0xDA, 0xAE, 0xD1, 0x8D, 0x77, 0xD5, 0xD3, 0xA3,
    ];

    private static readonly Lazy<TelltaleBlowfish> Cipher = new(() => new TelltaleBlowfish(DefinitiveSeriesKey));

    // Latin-1 maps every byte to one char and back, so untouched bytes survive an edit exactly.
    private static readonly Encoding Bytes = Encoding.Latin1;

    /// <summary>True for a file name the game treats as a resource description.</summary>
    public static bool IsResourceDescription(string fileName) =>
        fileName.StartsWith("_resdesc_", StringComparison.OrdinalIgnoreCase)
        && (fileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".lenc", StringComparison.OrdinalIgnoreCase));

    /// <summary>The plain Lua text of a resource description, or null when it can't be read.</summary>
    public static string? ReadText(string path)
    {
        try
        {
            return Decode(File.ReadAllBytes(path), out _);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The <c>set.priority</c> value, or null when the file can't be read or has none.</summary>
    public static int? ReadPriority(string path) =>
        ReadText(path) is { } text && PriorityPattern().Match(text) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var p)
            ? p
            : null;

    /// <summary>Sets <c>set.priority</c>, keeping the file plain or encrypted as it was.</summary>
    /// <exception cref="InvalidDataException">The file can't be decrypted or has no priority line.</exception>
    public static void WritePriority(string path, int priority)
    {
        var text = Decode(File.ReadAllBytes(path), out var encrypted)
            ?? throw new InvalidDataException($"{Path.GetFileName(path)} isn't a resource description the launcher can read.");
        var match = PriorityPattern().Match(text);
        if (!match.Success)
            throw new InvalidDataException($"{Path.GetFileName(path)} has no set.priority line.");

        var number = match.Groups[1];
        var edited = string.Concat(text.AsSpan(0, number.Index), priority.ToString(), text.AsSpan(number.Index + number.Length));
        var bytes = Encode(edited, encrypted);

        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        FileOps.Move(temp, path, overwrite: true);
    }

    internal static byte[] Encrypt(string text) => Encode(text, encrypted: true);

    private static string? Decode(byte[] data, out bool encrypted)
    {
        encrypted = data.AsSpan().StartsWith(EncryptedHeader);
        if (!encrypted)
            return data.Length > 0 && data[0] == 0x1B ? null : Bytes.GetString(data); // other ESC formats: compiled Lua or an older cipher

        var body = data.AsSpan(EncryptedHeader.Length).ToArray();
        Cipher.Value.Decrypt(body);
        var text = Bytes.GetString(body);
        // A wrong key gives noise; a resource description always registers a set.
        return text.Contains("RegisterSetDescription", StringComparison.Ordinal) ? text : null;
    }

    private static byte[] Encode(string text, bool encrypted)
    {
        var body = Bytes.GetBytes(text);
        if (!encrypted)
            return body;
        Cipher.Value.Encrypt(body);
        return [.. EncryptedHeader, .. body];
    }

    [GeneratedRegex(@"set\.priority\s*=\s*(-?\d+)")]
    private static partial Regex PriorityPattern();
}
