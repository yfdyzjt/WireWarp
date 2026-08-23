using System.IO.Pipes;

namespace WireWarp.Backend.Reference;

public static class Transport
{
    private const uint Magic = 0xBADBEEF;
    private const ushort Version = 1;
    private const string PipeName = "WireWarp";

    private static long _sendId;
    private static long _lastId;

    private static NamedPipeClientStream? _pipe;
    public static bool IsOpen => _pipe?.IsConnected ?? false;

    public static void Open()
    {
        _sendId = 0;
        _lastId = 0;

        _pipe = new NamedPipeClientStream(
            ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        _pipe.Connect(-1);
    }

    public static void Close()
    {
        _sendId = 0;
        _lastId = 0;
        
        _pipe?.Dispose();
        _pipe = null;
    }

    public static void ReadRequest()
    {
        var (tag, id, body) = ReadMessage();

        if (_lastId != 0 && id != _lastId + 1)
            throw new InvalidDataException($"Message gap detected: expected {_lastId + 1}, got {id}");
        _lastId = id;

        Handle(tag, body);
    }

    private static void Handle(Tag tag, byte[] body)
    {
        int status;
        string message;
        byte[]? payload;

        try
        {
            switch (tag)
            {
                case Tag.Startup: (status, message, payload) = Runtime.Startup(); break;
                case Tag.SyncTo: (status, message, payload) = Runtime.SyncTo(body); break;
                case Tag.SyncFrom: (status, message, payload) = Runtime.SyncFrom(); break;
                case Tag.Reset: (status, message, payload) = Runtime.Reset(); break;
                case Tag.Frame: (status, message, payload) = Runtime.Frame(body); break;
                case Tag.Shutdown: (status, message, payload) = Runtime.Shutdown(); break;
                default:
                    Console.WriteLine($"[ERROR] unknown tag {(ushort)tag}");
                    status = 1;
                    message = $"Unknown tag {(ushort)tag}";
                    payload = null;
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] Handler failed for tag {(ushort)tag}: {e}");
            status = 1;
            message = e.Message;
            payload = null;
        }

        SendAck(tag, status, message, payload);
    }

    private static void SendAck(Tag requestTag, int status, string message, byte[]? payload = null)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(status);
        w.Write(message);
        if (payload != null)
            w.Write(payload);

        WriteMessage((Tag)((ushort)requestTag + 1), ms.ToArray());
    }

    private static (Tag tag, long messageId, byte[] body) ReadMessage()
    {
        if (!IsOpen) throw new InvalidOperationException("Transport not open");

        var header = new byte[20];
        _pipe!.ReadExactly(header);

        using var ms = new MemoryStream(header);
        using var r = new BinaryReader(ms);

        var magic = r.ReadUInt32();
        var version = r.ReadUInt16();
        var tag = r.ReadUInt16();
        var id = r.ReadInt64();
        var length = r.ReadInt32();

        if (magic != Magic) throw new InvalidDataException("Header magic mismatch");
        if (version != Version) throw new InvalidDataException("Header version mismatch");

        var body = new byte[length];
        _pipe!.ReadExactly(body);

        return ((Tag)tag, id, body);
    }

    private static void WriteMessage(Tag tag, byte[] body)
    {
        if (!IsOpen) throw new InvalidOperationException("Transport not open");

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
