using System.Diagnostics;

namespace WireWarp.Backend.Reference;

public static class Runtime
{
    private static bool _synced;
    private static bool _started;
    private static string _path = "";

    public static (int status, string message, byte[]? payload) Startup()
    {
        Console.WriteLine("[INFO] simulation session begin");
        if (!_synced)
            Console.WriteLine("[WARNING] no sync from received before startup");

        _started = true;

        return (0, "", null);
    }

    public static (int status, string message, byte[]? payload) SyncTo(byte[] body)
    {
        using var r = new BinaryReader(new MemoryStream(body));

        var hash = r.ReadBytes(32);
        var path = r.ReadString();

        Console.WriteLine($"[INFO] sync from hash={Convert.ToHexString(hash)} path={path}");

        _synced = true;
        _path = path;

        return (0, "", null);
    }

    public static (int status, string message, byte[]? payload) SyncFrom()
    {
        Console.WriteLine($"[INFO] sync to write state to path={_path}");

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(new byte[32]);
        w.Write(_path);

        return (0, "", ms.ToArray());
    }

    public static (int status, string message, byte[]? payload) Reset()
    {
        Console.WriteLine("[INFO] restore initial state");

        return (0, "", null);
    }

    public static (int status, string message, byte[]? payload) Frame(byte[] body)
    {
        using var ms = new MemoryStream(body);
        using var r = new BinaryReader(ms);

        var run = r.ReadBoolean();
        var tick = r.ReadInt64();

        var count = r.ReadInt32();
        var pairs = new List<(int portId, int count)>(count);
        for (var i = 0; i < count; i++)
            pairs.Add((r.ReadInt32(), r.ReadInt32()));

        if (!_started)
            Console.WriteLine("[WIRING] no startup received, simulation not running");

        if (count != 0)
        {
            var inputs = string.Join(", ", pairs.Select(p => $"{p.portId}x{p.count}"));
            Console.WriteLine($"[INFO] run={run} tick={tick} count={count} inputs=[{inputs}]");
        }

        using var ackMs = new MemoryStream();
        using var ackW = new BinaryWriter(ackMs);

        ackW.Write(count);
        for (var i = 0; i < count; i++)
        {
            ackW.Write(pairs[i].portId);
            ackW.Write(pairs[i].count);
        }

        return (0, "", ackMs.ToArray());
    }

    public static (int status, string message, byte[]? payload) Shutdown()
    {
        Console.WriteLine("[INFO] session end");

        _started = false;
        _synced = false;

        return (0, "", null);
    }
}
