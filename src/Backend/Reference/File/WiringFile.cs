using WireWarp.Backend.Reference.Data;

namespace WireWarp.Backend.Reference.File;

public static class WiringFile
{
    private const uint Magic = 0xBADBEEF;
    private const uint Version = 1;
    private const int HashSize = 32;
    private const int GroupCount = 6;

    public static bool MatchHash(string path, byte[] hash)
    {
        if (!System.IO.File.Exists(path)) return false;
        try {
            using var fs = new FileStream(path, FileMode.Open);
            using var r = new BinaryReader(fs);
            return ReadHeader(r).AsSpan().SequenceEqual(hash);
        } catch { return false; }
    }

    public static bool Load(string path)
    {
        try
        {
            WiringGraph.Clean();

            using var fs = new FileStream(path, FileMode.Open);
            using var r = new BinaryReader(fs);
            WiringGraph.SetHash(ReadHeader(r));
            Deserialize(r);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] WiringFile.Load failed: {e}");
            WiringGraph.Clean();
            return false;
        }
    }

    public static bool Save(string path)
    {
        try
        {
            var temp = path + ".tmp";

            using (var fs = new FileStream(temp, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                WriteHeader(w, WiringGraph.Hash);
                WriteGroups(w);
            }

            System.IO.File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR] WiringFile.Save failed: {e}");
            return false;
        }
    }

    private static void WriteHeader(BinaryWriter w, ReadOnlySpan<byte> hash)
    {
        w.Write(Magic);
        w.Write(Version);
        w.Write(hash);
    }

    private static byte[] ReadHeader(BinaryReader r)
    {
        if (r.ReadUInt32() != Magic) throw new InvalidDataException("Header magic mismatch");
        if (r.ReadUInt32() != Version) throw new InvalidDataException("Header version mismatch");
        return r.ReadBytes(HashSize);
    }

    private static void WriteGroups(BinaryWriter w)
    {
        w.Write(GroupCount);
        var groupStartPos = w.BaseStream.Position;
        for (var i = 0; i < GroupCount; i++)
            w.Write(0);

        var starts = new long[GroupCount];

        starts[0] = WriteNodes(w, WiringGraph.InputPorts);
        starts[1] = WriteNodes(w, WiringGraph.OutputPorts);
        starts[2] = WriteNodes(w, WiringGraph.Lamps);
        starts[3] = WriteNodes(w, WiringGraph.Gates);
        starts[4] = WriteNodes(w, WiringGraph.Wires);
        starts[5] = w.BaseStream.Position;

        w.BaseStream.Position = groupStartPos;
        for (var i = 0; i < GroupCount; i++)
            w.Write((uint)starts[i]);
    }

    private static long WriteNodes<T>(BinaryWriter w, IReadOnlyList<T> nodes) where T : IConnectable
    {
        var start = w.BaseStream.Position;

        w.Write(nodes.Count);
        foreach (var node in nodes.OrderBy(n => n.Id))
        {
            w.Write(node.Type);
            w.Write(node.Id);

            if (node is OutputPort) { w.Write(0); continue; }

            var fanoutIds = node.Fanout.Select(n => n.Id).OrderBy(id => id).ToList();
            w.Write(fanoutIds.Count);
            foreach (var id in fanoutIds)
                w.Write(id);
        }

        return start;
    }

    private enum Group { InputPorts, OutputPorts, Lamps, Gates, Wires }

    private static void Deserialize(BinaryReader r)
    {
        if (r.ReadInt32() != GroupCount) throw new InvalidDataException("Wiring group count mismatch");

        var starts = new int[GroupCount];
        for (var i = 0; i < GroupCount; i++)
            starts[i] = r.ReadInt32();

        var edges = new List<(int fromId, int toId)>();

        if (ReadNodes(r, edges, Group.InputPorts) != starts[1]) throw new InvalidDataException("Wiring group 0 length mismatch");
        if (ReadNodes(r, edges, Group.OutputPorts) != starts[2]) throw new InvalidDataException("Wiring group 1 length mismatch");
        if (ReadNodes(r, edges, Group.Lamps) != starts[3]) throw new InvalidDataException("Wiring group 2 length mismatch");
        if (ReadNodes(r, edges, Group.Gates) != starts[4]) throw new InvalidDataException("Wiring group 3 length mismatch");
        if (ReadNodes(r, edges, Group.Wires) != starts[5]) throw new InvalidDataException("Wiring group 4 length mismatch");

        foreach (var (fromId, toId) in edges)
            WiringGraph.AddEdge(WiringGraph.Components[fromId], WiringGraph.Components[toId]);
    }

    private static long ReadNodes(BinaryReader r, List<(int fromId, int toId)> edges, Group group)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var type = r.ReadByte();
            var id = r.ReadInt32();

            var fanoutCount = r.ReadInt32();
            for (var j = 0; j < fanoutCount; j++)
                edges.Add((id, r.ReadInt32()));

            switch (group)
            {
                case Group.InputPorts:
                    WiringGraph.AddNode(new InputPort { Id = id, Type = (InputID)type }); break;
                case Group.OutputPorts:
                    WiringGraph.AddNode(new OutputPort { Id = id, Type = (OutputID)type }); break;
                case Group.Lamps:
                    WiringGraph.AddNode(new Lamp { Id = id, Type = (LampID)type }); break;
                case Group.Gates:
                    WiringGraph.AddNode(new Gate { Id = id, Type = (GateID)type }); break;
                case Group.Wires:
                    WiringGraph.AddNode(new Wire { Id = id, Type = (WireID)type }); break;
            }
        }
        return r.BaseStream.Position;
    }
}
