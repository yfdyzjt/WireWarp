namespace WireWarp.Frontend.Shared.File;

public static class HeaderFile
{
    public const uint Magic = 0xBADBEEF;
    public const uint Version = 1;
    public const int HashSize = 32;

    public static void Write(BinaryWriter w, ReadOnlySpan<byte> hash)
    {
        w.Write(Magic);
        w.Write(Version);
        w.Write(hash);
    }

    public static byte[] Read(BinaryReader r)
    {
        if (r.ReadUInt32() != Magic) throw new InvalidDataException("Header magic mismatch");
        if (r.ReadUInt32() != Version) throw new InvalidDataException($"Header version mismatch");

        return r.ReadBytes(HashSize);
    }

    public static bool MatchHash(string path, byte[] hash)
    {
        if (!System.IO.File.Exists(path)) return false;
        try
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var r = new BinaryReader(fs);
            return Read(r).AsSpan().SequenceEqual(hash);
        }
        catch { return false; }
    }
}
