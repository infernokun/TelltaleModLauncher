using System.Text;
using TelltaleModLauncher.Core;

namespace TelltaleModLauncher.Tests;

public class ResourceDescriptionTests
{
    // _resdesc_50_Mod_S2Rewind.lua from the Season 2 Rewind mod (MIT), encrypted by ttarchext
    // 0.3.2 for The Walking Dead: The Telltale Definitive Series. Its priority is 700.
    private const string EncryptedByTtarchext =
        "G0xFb0N0VXMp1pJYJg2X2eMs6FqBcDuRPKVpetSC7byjchRVUhs+b5/ACLAJ+3iN8ZpADTpiv+NhEr/9SQuTBZoNICIw2L6KlYk/" +
        "MZXrd+K7depMjKqHb0M3mw4aLUeAY2ZXvsMQbk6Cs7HsQI0kDrHVreMT5XA8IsCblcY+rUS/8BwScVUeaw6h5ePd4PKdbVL8b8Iu" +
        "ow8wT4qTuG5Ud6vI9nRaLtCRbJY4NKDixQiwHTxqZPeHoO1vUDIPQRDvMe/VbHhW0mzeG7W/U1l60/totNAYscm4wk160Tnyaf1E" +
        "etVu+NdYWjn22VAwNUF3ZyMaLSldZGtDk2kidHe3+3gsw509jLRL/tegYxEoK2/xlgzFHqTFNUoq1RcV24ON/HurW4PfxxJCPzRD" +
        "/DL/MdudXI+58CJgstnCQ0F+an1yHFARxaaaBiYFGg75aBrI0JgCAT5kotvlCwnnUtaFkk8AYjnn8jxndRhqNLa17xPGwA5TG1Ka" +
        "WoNzAvrzsTtcqiHwvPksG2znplGh7fkpyx+G2+SmqCdbmFHD/W5Wdj2NIW8oyh+oddsYNSVPgWLVeTI1expmNm+RgBA4nr7WnvGa" +
        "Pr8wNUF3ZyMaLXk9pE5FSvCiPgS6vqetdZNkOwHYHZ/OY4iWizx1jUalvK/+qi2CASbHjsP2QeUFa2qW6ut6Rvgx5SzKTted+2Ii" +
        "lBm9m6JCd7MuTa+YDFtTk57/JNj4pLVhJBr6W1BNeIUVwfEUqeSgfi9UHc5tt5yw1F2q8XB10oTRSPtkOXY8M2VELFhdrvr3XRzj" +
        "pbg2amkyCdOdBZHzFRv8NlC42d3tEIrfjGBzw6BIfFl/oX4BsKmiGJA1rZ3gB2rDUCYN2JgkZbXjfBe3fiQYH6MBvwTrATMSQ5VF" +
        "DM5HQ+wOnQZmrVWvF1uz7R4G2+la1C2IiCjwgQvTQyCNhmhyhlctvSLzSnhLI9X51V4gidxYecY5TysE7JtK4gzSYXftJwTEiKWI" +
        "XZaWLSBX9iAufUwg/UA0R8DyZ437KHR3t/t4LMOd0j8UV/D21I7WJG0JQAP0kL2KBGp/7Bcd5Vak4fq48oXJkPWEZ17COtWuaIV5" +
        "le5UXc4/eGK/tTcajAxSskYWNy3c0ZXyaD1BdCkK";

    private static string WriteEncrypted(TestFolder folder)
    {
        var path = Path.Combine(folder.Root, "_resdesc_50_Mod_S2Rewind.lua");
        File.WriteAllBytes(path, Convert.FromBase64String(EncryptedByTtarchext));
        return path;
    }

    [Fact]
    public void Decrypts_a_file_ttarchext_encrypted()
    {
        using var folder = new TestFolder();
        var path = WriteEncrypted(folder);

        var text = ResourceDescription.ReadText(path);

        Assert.NotNull(text);
        Assert.StartsWith("local set = {}", text);
        Assert.Contains("set.logicalDestination = \"<MenuSeason2>\"", text);
        Assert.Contains("RegisterSetDescription(set)", text);
        Assert.Equal(700, ResourceDescription.ReadPriority(path));
    }

    [Fact]
    public void Rewriting_the_same_priority_gives_back_ttarchexts_bytes()
    {
        using var folder = new TestFolder();
        var path = WriteEncrypted(folder);

        ResourceDescription.WritePriority(path, 700);

        Assert.Equal(Convert.FromBase64String(EncryptedByTtarchext), File.ReadAllBytes(path));
    }

    [Fact]
    public void A_new_priority_stays_encrypted_and_changes_nothing_else()
    {
        using var folder = new TestFolder();
        var path = WriteEncrypted(folder);
        var before = ResourceDescription.ReadText(path)!;

        ResourceDescription.WritePriority(path, 1250);

        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith("\u001bLEo"u8));
        Assert.Equal(1250, ResourceDescription.ReadPriority(path));
        Assert.Equal(before.Replace("set.priority = 700", "set.priority = 1250"), ResourceDescription.ReadText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void A_plain_file_stays_plain()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Root, "_resdesc_50_Plain.lua");
        const string text = "local set = {}\r\nset.priority = 900\r\nset.gameDataPriority = 900\r\nset.descriptionPriority = 0\r\n";
        File.WriteAllText(path, text, Encoding.Latin1);

        ResourceDescription.WritePriority(path, 950);

        // Only set.priority changes, not the other *Priority fields, and the line endings stay.
        Assert.Equal(text.Replace("set.priority = 900", "set.priority = 950"), File.ReadAllText(path, Encoding.Latin1));
    }

    [Fact]
    public void Files_that_cant_be_read_have_no_priority()
    {
        using var folder = new TestFolder();
        var olderCipher = Path.Combine(folder.Root, "older.lua");
        File.WriteAllBytes(olderCipher, [0x1B, 0x4C, 0x45, 0x6E, 0x01, 0x02]);
        var wrongKey = Path.Combine(folder.Root, "wrongkey.lua");
        File.WriteAllBytes(wrongKey, [.. "\u001bLEo"u8, .. Enumerable.Range(0, 64).Select(i => (byte)i)]);
        var noPriority = Path.Combine(folder.Root, "nopriority.lua");
        File.WriteAllText(noPriority, "GameEngine_AddBuildVersionInfo(\"x\")");

        Assert.Null(ResourceDescription.ReadPriority(olderCipher));
        Assert.Null(ResourceDescription.ReadPriority(wrongKey));
        Assert.Null(ResourceDescription.ReadPriority(noPriority));
        Assert.Throws<InvalidDataException>(() => ResourceDescription.WritePriority(wrongKey, 1));
        Assert.Throws<InvalidDataException>(() => ResourceDescription.WritePriority(noPriority, 1));
    }
}
