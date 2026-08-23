using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.File;

public static partial class IOFile
{
    private static long ReadTeleporter(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var portId = r.ReadInt32();
            var sx = r.ReadInt32();
            var sy = r.ReadInt32();
            var tx = r.ReadInt32();
            var ty = r.ReadInt32();

            IOExtra.SetTeleporter(portId, ((sx, sy), (tx, ty)));
        }

        return r.BaseStream.Position;
    }

    private static long ReadPumps(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var portId = r.ReadInt32();

            var inletCount = r.ReadInt32();
            var inlets = new List<(int x, int y)>(inletCount);
            for (var j = 0; j < inletCount; j++)
                inlets.Add((r.ReadInt32(), r.ReadInt32()));

            var outletCount = r.ReadInt32();
            var outlets = new List<(int x, int y)>(outletCount);
            for (var j = 0; j < outletCount; j++)
                outlets.Add((r.ReadInt32(), r.ReadInt32()));

            IOExtra.SetPump(portId, inlets, outlets);
        }

        return r.BaseStream.Position;
    }

    private static long ReadWireBulb(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var portId = r.ReadInt32();
            var type = (WireID)r.ReadByte();

            IOExtra.SetWireBulb(portId, type);
        }

        return r.BaseStream.Position;
    }

    private static long WriteTeleporter(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var teleporter = IOExtra.Teleporter.OrderBy(kv => kv.Key).ToList();

        w.Write(teleporter.Count);
        foreach (var (portId, (source, target)) in teleporter)
        {
            w.Write(portId);
            w.Write(source.x);
            w.Write(source.y);
            w.Write(target.x);
            w.Write(target.y);
        }

        return start;
    }

    private static long WritePumps(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var pumps = IOExtra.Pumps.OrderBy(kv => kv.Key).ToList();

        w.Write(pumps.Count);
        foreach (var (portId, (inlets, outlets)) in pumps)
        {
            w.Write(portId);

            w.Write(inlets.Count);
            foreach (var (x, y) in inlets)
            {
                w.Write(x);
                w.Write(y);
            }

            w.Write(outlets.Count);
            foreach (var (x, y) in outlets)
            {
                w.Write(x);
                w.Write(y);
            }
        }

        return start;
    }

    private static long WriteWireBulb(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var wireBulb = IOExtra.WireBulb.OrderBy(kv => kv.Key).ToList();

        w.Write(wireBulb.Count);
        foreach (var (portId, type) in wireBulb)
        {
            w.Write(portId);
            w.Write((byte)type);
        }

        return start;
    }
}
