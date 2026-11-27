using System.Buffers.Binary;
using System.Collections;
using System.Runtime.InteropServices;

namespace WireWarp.Frontend.Shared;

// Borrowed receive-buffer view. Consume synchronously BEFORE sending the next request.
internal sealed class CompactOutputRuns : IReadOnlyList<(int portId, int count)>
{
    private readonly ReadOnlyMemory<byte> _ids;
    private CompactOutputRuns(ReadOnlyMemory<byte> ids) => _ids = ids;
    public int Count => _ids.Length / 2;
    internal ReadOnlySpan<ushort> NativeIds => MemoryMarshal.Cast<byte, ushort>(_ids.Span);
    public (int portId, int count) this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return (BinaryPrimitives.ReadUInt16LittleEndian(_ids.Span.Slice(index * 2, 2)), 1);
        }
    }
    internal static CompactOutputRuns Parse(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < 4) throw new InvalidDataException("Truncated compact frame");
        int marker = BinaryPrimitives.ReadInt32LittleEndian(payload.Span);
        if (marker >= 0 || (long)(~marker) * 2 != payload.Length - 4)
            throw new InvalidDataException("Invalid compact frame count/length");
        return new CompactOutputRuns(payload[4..]);
    }
    public IEnumerator<(int portId, int count)> GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return this[i];
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
