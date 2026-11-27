using System.Buffers.Binary;

namespace WireWarp.Frontend.Shared;

internal static class FrameRunDecoder
{
    // The existing protocol is int32 count followed by count pairs of int32 values, little endian.
    // Reference decoder ignores trailing bytes, and does not reinterpret the run counts.
    internal static bool TryDecode(ReadOnlySpan<byte> body, out List<(int portId, int count)> runs)
    {
        runs = null!;
        if (body.Length < 4) return false;
        int count = BinaryPrimitives.ReadInt32LittleEndian(body);
        if (count < 0 || count > (body.Length - 4) / 8) return false;
        runs = new List<(int portId, int count)>(count);
        return TryDecodeInto(body, runs);
    }

    // Validate before clearing so malformed data cannot leave a partially decoded list.
    internal static bool TryDecodeInto(ReadOnlySpan<byte> body, List<(int portId, int count)> runs)
    {
        if (body.Length < 4) return false;
        int count = BinaryPrimitives.ReadInt32LittleEndian(body);
        if (count < 0 || count > (body.Length - 4) / 8) return false;
        runs.Clear();
        runs.EnsureCapacity(count);
        body = body[4..];
        for (int i = 0; i < count; i++)
        {
            int port = BinaryPrimitives.ReadInt32LittleEndian(body);
            int repeat = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
            runs.Add((port, repeat));
            body = body[8..];
        }
        return true;
    }
}
