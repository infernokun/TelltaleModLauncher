using System.Buffers.Binary;

namespace TelltaleModLauncher.Core;

/// <summary>
/// The Blowfish variant Telltale Tool uses for scripts in .ttarch2 games. It is standard
/// Blowfish with three changes: one S-box entry is byte-swapped before the key schedule, the
/// rounds read P[3], P[4], P[1], P[2] in place of P[1] to P[4], and decryption finishes with
/// P[3] instead of P[1]. The key schedule itself uses the standard rounds. Blocks are two
/// little-endian 32-bit words, and bytes after the last whole block are left as they are.
/// </summary>
/// <remarks>Matches ttarchext 0.3.2 by Luigi Auriemma (blowfish_ttarch.c, "version 7").</remarks>
internal sealed class TelltaleBlowfish
{
    private const int Rounds = 16;
    private readonly uint[] p = new uint[Rounds + 2];
    private readonly uint[][] s = new uint[4][];

    public TelltaleBlowfish(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
            throw new ArgumentException("The key is empty.", nameof(key));

        BlowfishTables.P.CopyTo(p, 0);
        s[0] = (uint[])BlowfishTables.S0.Clone();
        s[1] = (uint[])BlowfishTables.S1.Clone();
        s[2] = (uint[])BlowfishTables.S2.Clone();
        s[3] = (uint[])BlowfishTables.S3.Clone();
        s[0][118] = BinaryPrimitives.ReverseEndianness(s[0][118]);

        var j = 0;
        for (var i = 0; i < p.Length; i++)
        {
            uint data = 0;
            for (var k = 0; k < 4; k++)
            {
                data = (data << 8) | key[j];
                j = (j + 1) % key.Length;
            }
            p[i] ^= data;
        }

        uint l = 0, r = 0;
        for (var i = 0; i < p.Length; i += 2)
        {
            EncipherStandard(ref l, ref r);
            p[i] = l;
            p[i + 1] = r;
        }
        foreach (var box in s)
        {
            for (var i = 0; i < 256; i += 2)
            {
                EncipherStandard(ref l, ref r);
                box[i] = l;
                box[i + 1] = r;
            }
        }
    }

    public void Encrypt(Span<byte> data) => Blocks(data, encrypt: true);

    public void Decrypt(Span<byte> data) => Blocks(data, encrypt: false);

    private void Blocks(Span<byte> data, bool encrypt)
    {
        for (var offset = 0; offset + 8 <= data.Length; offset += 8)
        {
            var block = data.Slice(offset, 8);
            var l = BinaryPrimitives.ReadUInt32LittleEndian(block);
            var r = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);
            if (encrypt)
                Encipher(ref l, ref r);
            else
                Decipher(ref l, ref r);
            BinaryPrimitives.WriteUInt32LittleEndian(block, l);
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], r);
        }
    }

    private uint F(uint x) =>
        ((s[0][x >> 24] + s[1][(x >> 16) & 0xFF]) ^ s[2][(x >> 8) & 0xFF]) + s[3][x & 0xFF];

    // Standard Blowfish, only used by the key schedule.
    private void EncipherStandard(ref uint xl, ref uint xr)
    {
        uint l = xl, r = xr;
        for (var i = 0; i < Rounds; i++)
        {
            l ^= p[i];
            r ^= F(l);
            (l, r) = (r, l);
        }
        (l, r) = (r, l);
        r ^= p[Rounds];
        l ^= p[Rounds + 1];
        xl = l;
        xr = r;
    }

    private void Encipher(ref uint xl, ref uint xr)
    {
        uint l = xl, r = xr;
        for (var i = 0; i < Rounds; i++)
        {
            l ^= i switch { 1 => p[3], 2 => p[4], 3 => p[1], 4 => p[2], _ => p[i] };
            r ^= F(l);
            (l, r) = (r, l);
        }
        (l, r) = (r, l);
        r ^= p[Rounds];
        l ^= p[Rounds + 1];
        xl = l;
        xr = r;
    }

    private void Decipher(ref uint xl, ref uint xr)
    {
        uint l = xl, r = xr;
        for (var i = Rounds + 1; i > 1; i--)
        {
            l ^= i switch { 4 => p[2], 3 => p[1], 2 => p[4], _ => p[i] };
            r ^= F(l);
            (l, r) = (r, l);
        }
        (l, r) = (r, l);
        r ^= p[3];
        l ^= p[0];
        xl = l;
        xr = r;
    }
}
