namespace TelltaleModLauncher.Tests;

/// <summary>Builds the smallest PE32+ file that carries an icon group, the way an exe stores its icon.</summary>
public static class FakeExe
{
    /// <summary>Each image is (size in pixels, 256 meaning 256, and its bytes).</summary>
    public static byte[] WithIcon(params (int Size, byte[] Data)[] images)
    {
        const uint sectionRva = 0x1000;
        const int sectionFile = 0x200;

        // Resource section: root, two type directories, one name directory per image and group,
        // their language directories, data entries, then the raw data.
        using var rsrc = new MemoryStream();
        var w = new BinaryWriter(rsrc);
        var dirSize = (int entries) => 16 + 8 * entries;

        var root = 0;
        var iconTypeDir = root + dirSize(2);
        var iconNameDirs = iconTypeDir + dirSize(images.Length);
        var groupTypeDir = iconNameDirs + images.Length * dirSize(1);
        var groupNameDir = groupTypeDir + dirSize(1);
        var groupLangDir = groupNameDir + dirSize(1);
        var iconLangDirs = groupLangDir + dirSize(1);
        var dataEntries = iconLangDirs + images.Length * dirSize(1);
        var rawStart = dataEntries + (images.Length + 1) * 16;

        var group = new MemoryStream();
        var g = new BinaryWriter(group);
        g.Write((ushort)0);
        g.Write((ushort)1);
        g.Write((ushort)images.Length);
        for (var i = 0; i < images.Length; i++)
        {
            var px = (byte)(images[i].Size >= 256 ? 0 : images[i].Size);
            g.Write(px);
            g.Write(px);
            g.Write((byte)0);
            g.Write((byte)0);
            g.Write((ushort)1);
            g.Write((ushort)32);
            g.Write(images[i].Data.Length);
            g.Write((ushort)(i + 1));
        }
        var groupData = group.ToArray();

        void Directory(int at, params (uint Id, int Offset, bool IsDirectory)[] entries)
        {
            rsrc.Position = at;
            w.Write(0u);
            w.Write(0u);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)entries.Length);
            foreach (var (id, offset, isDirectory) in entries)
            {
                w.Write(id);
                w.Write((uint)offset | (isDirectory ? 0x80000000 : 0));
            }
        }

        Directory(root, (3, iconTypeDir, true), (14, groupTypeDir, true));
        Directory(iconTypeDir, images.Select((_, i) => ((uint)(i + 1), iconNameDirs + i * dirSize(1), true)).ToArray());
        for (var i = 0; i < images.Length; i++)
        {
            Directory(iconNameDirs + i * dirSize(1), (0x409, iconLangDirs + i * dirSize(1), true));
            Directory(iconLangDirs + i * dirSize(1), (0x409, dataEntries + (i + 1) * 16, false));
        }
        Directory(groupTypeDir, (1, groupNameDir, true));
        Directory(groupNameDir, (0x409, groupLangDir, true));
        Directory(groupLangDir, (0x409, dataEntries, false));

        var raw = rawStart;
        void DataEntry(int at, byte[] data)
        {
            rsrc.Position = at;
            w.Write(sectionRva + (uint)raw);
            w.Write(data.Length);
            w.Write(0u);
            w.Write(0u);
            rsrc.Position = raw;
            w.Write(data);
            raw += data.Length;
        }

        DataEntry(dataEntries, groupData);
        for (var i = 0; i < images.Length; i++)
            DataEntry(dataEntries + (i + 1) * 16, images[i].Data);
        var section = rsrc.ToArray();

        using var file = new MemoryStream();
        var f = new BinaryWriter(file);
        f.Write((ushort)0x5A4D);
        file.Position = 0x3C;
        f.Write(0x40u);
        file.Position = 0x40;
        f.Write(0x00004550u);
        f.Write((ushort)0x8664);  // machine
        f.Write((ushort)1);       // sections
        f.Write(new byte[12]);
        f.Write((ushort)240);     // optional header size
        f.Write((ushort)0x22);
        var optional = file.Position;
        f.Write((ushort)0x20B);   // PE32+
        file.Position = optional + 112 + 2 * 8;
        f.Write(sectionRva);
        f.Write(section.Length);
        file.Position = optional + 240;
        f.Write(".rsrc\0\0\0"u8.ToArray());
        f.Write(section.Length);
        f.Write(sectionRva);
        f.Write(section.Length);
        f.Write(sectionFile);
        f.Write(new byte[16]);
        file.Position = sectionFile;
        f.Write(section);
        return file.ToArray();
    }
}
