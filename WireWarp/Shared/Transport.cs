#nullable enable annotations
using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.IO.Pipes;

namespace WireWarp.Frontend.Shared;

public static class Transport
{
    private const uint Magic = 0xBADBEEF;
    private const ushort Version = 1;
    private const string PipeName = "WireWarp";

    private static long _sendId;
    private static long _lastId;

    private static long _sendTime;
    private static long _ackTime;
    private static long _bodyTime;
    public static long LastCompletedFrameId { get; private set; }
    public static long LastSentFrameId { get; private set; }
    public static double LastFrameBodyLatency { get; private set; }
    public static double LastFrameDecodeMs { get; private set; }

    private static NamedPipeServerStream? _pipe;
    private static Task<(Tag tag, long messageId, ReadOnlyMemory<byte> body)>? _pendingFrame;
    // Per-connection storage: a disconnected reader cannot write into a new connection's buffers.
    private sealed class ReceiveStorage
    {
        internal readonly byte[] Header = new byte[20];
        private byte[] _body = [];
        internal Memory<byte> Body(int length)
        {
            if (length < 0) throw new InvalidDataException("Negative body length");
            if (length > 16 * 1024 * 1024) return new byte[length]; // do not retain unusually large messages
            if (_body.Length < length) _body = new byte[Math.Min(16 * 1024 * 1024, Math.Max(length, Math.Max(4096, _body.Length * 2)))];
            return _body.AsMemory(0, length);
        }
    }
    private static ReceiveStorage _receive = new();
    private static List<(int portId, int count)> _decoded = new();
    private static bool _pendingCompiled;
    private static Tag _pendingTag = Tag.Frame;
    public static bool CompactSupported { get; private set; }
    public static string BackendMode { get; private set; } = "unknown (not reported)";
    public static long OutputPayloadBytes { get; private set; }
    public static long CompactFrames { get; private set; }
    public static long RleFrames { get; private set; }
    public static string EncodingStatus => CompactSupported ? "compact16-supported" : "legacy-rle-only";

    public static bool IsOpen => _pipe?.IsConnected ?? false;
    public static double LatencyTime => Stopwatch.GetElapsedTime(_sendTime, _ackTime).TotalMilliseconds;

    public static void Open()
    {
        Close();

        try
        {
            _pipe = new NamedPipeServerStream(
                PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _pipe.WaitForConnection();
        }
        catch (Exception e)
        {
            Close();
            throw new Exception($"Failed to open backend pipe: {e.Message}", e);
        }
    }

    public static void Close()
    {
        _sendId = 0;
        _lastId = 0;

        _sendTime = 0;
        _ackTime = 0;
        _bodyTime = 0;
        LastCompletedFrameId = LastSentFrameId = 0;
        LastFrameBodyLatency = LastFrameDecodeMs = 0;

        _pendingFrame = null;
        _pendingCompiled = false;
        _pendingTag = Tag.Frame;
        CompactSupported = false;
        BackendMode = "unknown (not reported)";
        OutputPayloadBytes = CompactFrames = RleFrames = 0;
        _receive = new();
        _decoded = new();

        _pipe?.Dispose();
        _pipe = null;
    }

    public static (int status, string message) SendStartup() 
    {
        try
        {
            var (ack, payload) = UnpackAck(Request(Tag.Startup, []));
            CompactSupported = ack.status == 0 && payload.AsSpan().SequenceEqual("WW38:compact16"u8);
            return ack;
        }
        catch (Exception e) { throw new Exception($"Backend startup failed: {e.Message}", e); }
    }         

    public static (int status, string message) SendSyncTo(byte[] hash, string path)
    {
        try
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(hash);
            w.Write(path);
            BackendMode = "unknown (not reported)";
            var (ack, payload) = UnpackAck(Request(Tag.SyncTo, ms.ToArray()));
            if (ack.status == 0)
            {
                string tag = System.Text.Encoding.ASCII.GetString(payload);
                BackendMode = tag switch {
                    "WW40:v4.1" => "v4.1", "WW40:v4.0" => "v4.0", "WW40:v3.5" => "v3.5", "WW40:v3.3" => "v3.3",
                    "WW40:v3.2" => "v3.2", "WW40:v3.1" => "v3.1", "WW40:v3" => "v3",
                    "WW40:aot" => "aot", "WW40:generic" => "generic", _ => "unknown (not reported)"
                };
            }
            return ack;
        }
        catch (Exception e) { throw new Exception($"Backend sync to failed: {e.Message}", e); }
    }

    public static ((int status, string message) ack, (byte[] hash, string path) payload) SendSyncFrom()
    {
        try
        {
            var (ack, payload) = UnpackAck(Request(Tag.SyncFrom, []));
            if (ack.status == 0)
            {
                using var ms = new MemoryStream(payload);
                using var r = new BinaryReader(ms);
                return (ack, (r.ReadBytes(32), r.ReadString()));
            }
            else
                return (ack, ([], "")); 
        }
        catch (Exception e) { throw new Exception($"Backend sync from failed: {e.Message}", e); }
    }

    public static (int status, string message) SendReset()
    {
        try { return UnpackAck(Request(Tag.Reset, [])).ack; }
        catch (Exception e) { throw new Exception($"Backend reset failed: {e.Message}", e); }
    }  

    public static (int status, string message) SendShutdown()
    {
        try { var ack = UnpackAck(Request(Tag.Shutdown, [])).ack; if (ack.status == 0) BackendMode = "unknown (not reported)"; return ack; }
        catch (Exception e) { throw new Exception($"Backend shutdown failed: {e.Message}", e); }
    }

    public static void SendFrameAsync(bool run, long tick, IReadOnlyList<(int portId, int count)> inputs)
    {
        try
        {
            if (!IsOpen) throw new InvalidOperationException("Transport not open");
            if (_pendingFrame != null) throw new InvalidOperationException("Frame already in flight");

            bool compact = Runtime.CompactFrontend && CompactSupported;
            _pendingTag = compact ? Tag.FrameCompact : Tag.Frame;
            WriteMessage(_pendingTag, PackFrame(run, tick, inputs));
            LastSentFrameId = _sendId;

            var pipe = _pipe!;
            var storage = Runtime.CompiledFrontend ? _receive : null;
            _pendingCompiled = storage != null;
            // A true async pipe read needs no new LongRunning thread per frame.
            // Keep v3.7 scheduling in compiled mode for an independent baseline.
            _pendingFrame = Runtime.AsyncFrontend ? ReadMessageAsync(pipe, storage) :
                Task.Factory.StartNew(() => ReadMessageAsync(pipe, storage), CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            _ = _pendingFrame.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception e) { throw new Exception($"Backend frame failed: {e.Message}", e); }
    }

    public static ((int status, string message) ack, IReadOnlyList<(int portId, int count)> payload) CompleteFrame()
    {
        try
        {
            LastCompletedFrameId = 0;
            LastFrameBodyLatency = LastFrameDecodeMs = 0;
            // Decoder selection follows the current mode; storage follows the actual sent request.
            Tag requestTag = _pendingTag;
            if (!TakeFrameAck(out var ack, out var payload))
                return ((0, ""), []);
            LastCompletedFrameId = _lastId;
            LastFrameBodyLatency = Math.Max(0, Stopwatch.GetElapsedTime(_sendTime, _bodyTime).TotalMilliseconds);
            if (ack.status != 0) return (ack, []);
            bool measure = FrontendDiagnostics.ProfileEnabled;
            long start = measure ? Stopwatch.GetTimestamp() : 0;
            // A compact request may return compact singletons or ordinary RLE.
            IReadOnlyList<(int portId, int count)> runs;
            // Avoid retaining a pathological giant list forever. Typical RGB responses are much smaller.
            if (_decoded.Capacity > 2_097_152) _decoded = new();
            OutputPayloadBytes += payload.Length;
            if (requestTag == Tag.FrameCompact && payload.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(payload.Span) < 0)
            {
                runs = CompactOutputRuns.Parse(payload);
                CompactFrames++;
            }
            else
            {
                RleFrames++;
                if (requestTag == Tag.FrameCompact && (payload.Length < 4 ||
                    (long)BinaryPrimitives.ReadInt32LittleEndian(payload.Span) * 8 != payload.Length - 4))
                    throw new InvalidDataException("Invalid compact-request RLE fallback length");
                if (Runtime.CompiledFrontend && FrameRunDecoder.TryDecodeInto(payload.Span, _decoded))
                    runs = _decoded; // valid until the next CompleteFrame; Runtime consumes synchronously
                else if (Runtime.FastFrontend && FrameRunDecoder.TryDecode(payload.Span, out var fastRuns))
                    runs = fastRuns;
                else runs = UnpackFrameAck(WholeArray(payload));
            }
            if (measure) LastFrameDecodeMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return (ack, runs);
        }
        catch (Exception e) { throw new Exception($"Backend frame failed: {e.Message}", e); }
    }

    private static byte[] Request(Tag tag, byte[] body)
    {
        if (!IsOpen) throw new InvalidOperationException("Transport not open");

        if (TakeFrameAck(out var ack, out _) && ack.status != 0)
            throw new Exception($"Backend frame failed: {ack.status} {ack.message}");

        WriteMessage(tag, body);
        var (respTag, id, respBody) = ReadMessageAsync(_pipe!, null).GetAwaiter().GetResult();

        CheckResponse(tag, respTag, id);

        return WholeArray(respBody);
    }

    private static bool TakeFrameAck(out (int status, string message) ack, out ReadOnlyMemory<byte> payload)
    {
        if (_pendingFrame == null)
        { ack = default; payload = ReadOnlyMemory<byte>.Empty; return false; }

        var task = _pendingFrame;
        _pendingFrame = null;

        var (respTag, id, respBody) = task.GetAwaiter().GetResult();

        CheckResponse(_pendingTag, respTag, id);

        bool compiled = _pendingCompiled;
        _pendingCompiled = false;
        if (compiled) (ack, payload) = UnpackAckView(respBody);
        else
        {
            var unpacked = UnpackAck(WholeArray(respBody));
            ack = unpacked.ack;
            payload = unpacked.payload;
        }
        return true;
    }

    private static void CheckResponse(Tag requestTag, Tag responseTag, long id)
    {
        if (_lastId != 0 && id != _lastId + 1)
            throw new InvalidDataException($"Message gap detected: expected {_lastId + 1}, got {id}");
        _lastId = id;

        var expected = (Tag)((ushort)requestTag + 1);
        if (responseTag != expected)
            throw new InvalidDataException($"Unexpected tag {(ushort)responseTag}, expected {(ushort)expected}");
    }

    private static void WriteMessage(Tag tag, byte[] body)
    {
        var id = Interlocked.Increment(ref _sendId);

        using var ms = new MemoryStream(20 + body.Length);
        using var w = new BinaryWriter(ms);

        w.Write(Magic);
        w.Write(Version);
        w.Write((ushort)tag);
        w.Write(id);

        w.Write(body.Length);
        w.Write(body);

        _pipe!.Write(ms.GetBuffer(), 0, (int)ms.Length);

        _sendTime = Stopwatch.GetTimestamp();
    }

    private static async Task<(Tag tag, long messageId, ReadOnlyMemory<byte> body)> ReadMessageAsync(
        NamedPipeServerStream pipe, ReceiveStorage? storage)
    {
        var header = storage?.Header ?? new byte[20];
        await pipe.ReadExactlyAsync(header).ConfigureAwait(false);
        if (ReferenceEquals(pipe, _pipe)) _ackTime = Stopwatch.GetTimestamp();

        uint magic; ushort version, tag; long id; int length;
        if (storage == null)
        {
            using var ms = new MemoryStream(header);
            using var r = new BinaryReader(ms);
            magic = r.ReadUInt32(); version = r.ReadUInt16(); tag = r.ReadUInt16(); id = r.ReadInt64(); length = r.ReadInt32();
        }
        else
        {
            magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
            version = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4));
            tag = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
            id = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8));
            length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
        }
        if (magic != Magic) throw new InvalidDataException("Header magic mismatch");
        if (version != Version) throw new InvalidDataException("Header version mismatch");
        if (length < 0 || length > 256 * 1024 * 1024) throw new InvalidDataException("Invalid body length");

        Memory<byte> body = storage == null ? new byte[length] : storage.Body(length);
        await pipe.ReadExactlyAsync(body).ConfigureAwait(false);
        if (ReferenceEquals(pipe, _pipe)) _bodyTime = Stopwatch.GetTimestamp();
        return ((Tag)tag, id, body);
    }

    private static byte[] WholeArray(ReadOnlyMemory<byte> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out var segment) && segment.Array != null &&
            segment.Offset == 0 && segment.Count == segment.Array.Length) return segment.Array;
        return memory.ToArray();
    }

    // Use the existing BinaryReader string semantics; expose only the valid payload slice.
    private static ((int status, string message) ack, ReadOnlyMemory<byte> payload) UnpackAckView(ReadOnlyMemory<byte> body)
    {
        if (!MemoryMarshal.TryGetArray(body, out var segment) || segment.Array == null)
            throw new InvalidDataException("Expected array-backed response");
        using var ms = new MemoryStream(segment.Array, segment.Offset, segment.Count, false);
        using var r = new BinaryReader(ms);
        int status = r.ReadInt32();
        string message = r.ReadString();
        return ((status, message), body[(int)ms.Position..]);
    }

    private static ((int status, string message) ack, byte[] payload) UnpackAck(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var r = new BinaryReader(ms);

        var status = r.ReadInt32();
        var message = r.ReadString();
        var payload = r.ReadBytes((int)(ms.Length - ms.Position));

        return ((status, message), payload);
    }

    private static byte[] PackFrame(bool run, long tick, IReadOnlyList<(int portId, int count)> inputs)
    {
        using var ms = new MemoryStream(13 + 8 * inputs.Count);
        using var w = new BinaryWriter(ms);

        w.Write(run);
        w.Write(tick);

        w.Write(inputs.Count);
        foreach (var (portId, count) in inputs)
        {
            w.Write(portId);
            w.Write(count);
        }

        return ms.ToArray();
    }

    private static List<(int portId, int count)> UnpackFrameAck(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var r = new BinaryReader(ms);

        var count = r.ReadInt32();
        var result = new List<(int, int)>(count);
        for (var i = 0; i < count; i++)
            result.Add((r.ReadInt32(), r.ReadInt32()));

        return result;
    }

    private enum Tag : ushort
    {
        Startup = 1, StartupAck = 2,
        SyncTo = 3, SyncToAck = 4,
        SyncFrom = 5, SyncFromAck = 6,
        Reset = 7, ResetAck = 8,
        Frame = 9, FrameAck = 10,
        Shutdown = 11, ShutdownAck = 12,
        FrameCompact = 13, FrameCompactAck = 14,
    }
}
