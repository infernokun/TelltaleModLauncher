namespace TelltaleModLauncher.Core;

/// <summary>
/// Reads the icon a Windows exe shows in Explorer straight from its resources, so it works on
/// any OS. Returns the largest image of the first icon group, as PNG bytes when the exe stores
/// it that way, or wrapped as a one-image .ico file otherwise.
/// </summary>
public static class ExeIcon
{
    private const uint RtIcon = 3;
    private const uint RtGroupIcon = 14;
    private const int MaxImageBytes = 4 * 1024 * 1024;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static byte[]? TryRead(string exePath)
    {
        try
        {
            using var stream = File.OpenRead(exePath);
            using var reader = new BinaryReader(stream);
            return Read(reader);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or EndOfStreamException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    public static bool IsPng(byte[] data) => data.AsSpan().StartsWith(PngSignature);

    private static byte[]? Read(BinaryReader reader)
    {
        var stream = reader.BaseStream;
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D) // "MZ"
            return null;

        stream.Position = 0x3C;
        var peOffset = reader.ReadUInt32();
        stream.Position = peOffset;
        if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
            return null;

        stream.Position += 2;
        var sectionCount = reader.ReadUInt16();
        stream.Position += 12;
        var optionalHeaderSize = reader.ReadUInt16();
        stream.Position += 2;

        var optionalHeader = stream.Position;
        var magic = reader.ReadUInt16();
        var dataDirectories = optionalHeader + magic switch { 0x10B => 96, 0x20B => 112, _ => -1 };
        if (dataDirectories < optionalHeader)
            return null;

        stream.Position = dataDirectories + 2 * 8; // the resource table is directory 2
        var resourceRva = reader.ReadUInt32();
        if (resourceRva == 0)
            return null;

        var sections = new List<(uint VirtualAddress, uint Size, uint RawPointer)>();
        stream.Position = optionalHeader + optionalHeaderSize;
        for (var i = 0; i < sectionCount; i++)
        {
            stream.Position += 8;
            var virtualSize = reader.ReadUInt32();
            var virtualAddress = reader.ReadUInt32();
            var rawSize = reader.ReadUInt32();
            var rawPointer = reader.ReadUInt32();
            stream.Position += 16;
            sections.Add((virtualAddress, Math.Max(virtualSize, rawSize), rawPointer));
        }

        long? ToOffset(uint rva)
        {
            foreach (var (address, size, raw) in sections)
            {
                if (rva >= address && rva < address + size)
                    return rva - address + raw;
            }
            return null;
        }

        if (ToOffset(resourceRva) is not { } root)
            return null;

        // Resource tree: type, then name or id, then language. Leaves hold an RVA and a size.
        List<(uint Id, uint Offset, bool IsDirectory)> Entries(long directory)
        {
            stream.Position = directory + 12;
            var count = reader.ReadUInt16() + reader.ReadUInt16();
            var entries = new List<(uint, uint, bool)>();
            for (var i = 0; i < Math.Min(count, 4096); i++)
            {
                var id = reader.ReadUInt32();
                var offset = reader.ReadUInt32();
                entries.Add((id, offset & 0x7FFFFFFF, (offset & 0x80000000) != 0));
            }
            return entries;
        }

        byte[]? FirstLeaf(uint offset, bool isDirectory)
        {
            for (var depth = 0; isDirectory && depth < 4; depth++)
            {
                var first = Entries(root + offset).FirstOrDefault();
                if (first == default)
                    return null;
                (offset, isDirectory) = (first.Offset, first.IsDirectory);
            }
            if (isDirectory)
                return null;

            stream.Position = root + offset;
            var dataRva = reader.ReadUInt32();
            var size = reader.ReadUInt32();
            if (size == 0 || size > MaxImageBytes || ToOffset(dataRva) is not { } dataOffset)
                return null;
            stream.Position = dataOffset;
            var bytes = reader.ReadBytes((int)size);
            return bytes.Length == size ? bytes : null;
        }

        var types = Entries(root);
        var groups = types.FirstOrDefault(t => t.Id == RtGroupIcon && t.IsDirectory);
        var icons = types.FirstOrDefault(t => t.Id == RtIcon && t.IsDirectory);
        if (groups == default || icons == default)
            return null;

        var firstGroup = Entries(root + groups.Offset).FirstOrDefault();
        if (firstGroup == default || FirstLeaf(firstGroup.Offset, firstGroup.IsDirectory) is not { Length: >= 6 } group)
            return null;

        // GRPICONDIR: 6 byte header, then 14 byte entries ending in the RT_ICON id.
        var count = BitConverter.ToUInt16(group, 4);
        (int Size, int Bits, ushort Id, byte[] Entry)? best = null;
        for (var i = 0; i < count && 6 + (i + 1) * 14 <= group.Length; i++)
        {
            var entry = group.AsSpan(6 + i * 14, 14).ToArray();
            var size = entry[0] == 0 ? 256 : entry[0];
            var bits = BitConverter.ToUInt16(entry, 6);
            if (best is not { } b || size > b.Size || (size == b.Size && bits > b.Bits))
                best = (size, bits, BitConverter.ToUInt16(entry, 12), entry);
        }
        if (best is not { } chosen)
            return null;

        var image = Entries(root + icons.Offset).FirstOrDefault(e => e.Id == chosen.Id);
        if (image == default || FirstLeaf(image.Offset, image.IsDirectory) is not { Length: > 0 } data)
            return null;

        if (IsPng(data))
            return data;

        // Wrap the bitmap as a one-image .ico so an image decoder can read it.
        var ico = new byte[6 + 16 + data.Length];
        BitConverter.GetBytes((ushort)1).CopyTo(ico, 2);
        BitConverter.GetBytes((ushort)1).CopyTo(ico, 4);
        chosen.Entry.AsSpan(0, 8).CopyTo(ico.AsSpan(6));
        BitConverter.GetBytes(data.Length).CopyTo(ico, 14);
        BitConverter.GetBytes(22).CopyTo(ico, 18);
        data.CopyTo(ico, 22);
        return ico;
    }
}
