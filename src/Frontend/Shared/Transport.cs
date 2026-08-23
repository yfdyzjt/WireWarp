using System.Diagnostics;
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

    private static NamedPipeServerStream? _pipe;
    private static Task<(Tag tag, long messageId, byte[] body)>? _pendingFrame;

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

        _pendingFrame = null;

        _pipe?.Dispose();
        _pipe = null;
    }

    public static (int status, string message) SendStartup() 
    {
        try { return UnpackAck(Request(Tag.Startup, [])).ack; }
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
            return UnpackAck(Request(Tag.SyncTo, ms.ToArray())).ack;
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
        try { return UnpackAck(Request(Tag.Shutdown, [])).ack; }
        catch (Exception e) { throw new Exception($"Backend shutdown failed: {e.Message}", e); }
    }

    public static void SendFrameAsync(bool run, long tick, IReadOnlyList<(int portId, int count)> inputs)
    {
        try
        {
            if (!IsOpen) throw new InvalidOperationException("Transport not open");
            if (_pendingFrame != null) throw new InvalidOperationException("Frame already in flight");

            WriteMessage(Tag.Frame, PackFrame(run, tick, inputs));

            _pendingFrame = Task.Run(ReadMessage);
            _ = _pendingFrame.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception e) { throw new Exception($"Backend frame failed: {e.Message}", e); }
    }

    public static ((int status, string message) ack, IReadOnlyList<(int portId, int count)> payload) CompleteFrame()
    {
        try
        {
            if (!TakeFrameAck(out var ack, out var payload))
                return ((0, ""), []);

            return ack.status == 0 ? (ack, UnpackFrameAck(payload)) : (ack, []);
        }
        catch (Exception e) { throw new Exception($"Backend frame failed: {e.Message}", e); }
    }

    private static byte[] Request(Tag tag, byte[] body)
    {
        if (!IsOpen) throw new InvalidOperationException("Transport not open");

        if (TakeFrameAck(out var ack, out _) && ack.status != 0)
            throw new Exception($"Backend frame failed: {ack.status} {ack.message}");

        WriteMessage(tag, body);
        var (respTag, id, respBody) = ReadMessage();

        CheckResponse(tag, respTag, id);

        return respBody;
    }

    private static bool TakeFrameAck(out (int status, string message) ack, out byte[] payload)
    {
        if (_pendingFrame == null)
        { ack = default; payload = []; return false; }

        var task = _pendingFrame;
        _pendingFrame = null;

        var (respTag, id, respBody) = task.GetAwaiter().GetResult();

        CheckResponse(Tag.Frame, respTag, id);

        (ack, payload) = UnpackAck(respBody);
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

    private static (Tag tag, long messageId, byte[] body) ReadMessage()
    {
        var header = new byte[20];
        _pipe!.ReadExactly(header);

        _ackTime = Stopwatch.GetTimestamp();

        uint magic; ushort version; ushort tag; long id; int length;

        using var ms = new MemoryStream(header);
        using var r = new BinaryReader(ms);

        magic = r.ReadUInt32();
        version = r.ReadUInt16();
        tag = r.ReadUInt16();
        id = r.ReadInt64();
        length = r.ReadInt32();

        if (magic != Magic) throw new InvalidDataException("Header magic mismatch");
        if (version != Version) throw new InvalidDataException("Header version mismatch");

        var body = new byte[length];
        _pipe.ReadExactly(body);
        return ((Tag)tag, id, body);
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
    }
}
