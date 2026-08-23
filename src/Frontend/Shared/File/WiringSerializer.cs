using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.File;

public static partial class WiringFile
{
    const int GroupCount = 6;

    public static void Serialize(BinaryWriter w)
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

    private static long WriteNodes<T>(BinaryWriter w, IReadOnlySet<T> nodes) where T : IConnectable
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

    public static void Deserialize(BinaryReader r)
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

            var components = WiringGraph.Components;
            switch (group)
            {
                case Group.InputPorts:
                    components[id] = WiringGraph.AddNode(new InputPort {Id = id}); break;
                case Group.OutputPorts:
                    components[id] = WiringGraph.AddNode(new OutputPort {Id = id}); break;
                case Group.Lamps:
                    components[id] = WiringGraph.AddNode(new Lamp {Id = id, Type = (LampID)type}); break;
                case Group.Gates:
                    components[id] = WiringGraph.AddNode(new Gate {Id = id, Type = (GateID)type}); break;
                case Group.Wires:
                    components[id] = WiringGraph.AddNode(new Wire {Id = id, Type = (WireID)type}); break;
            }
        }
        return r.BaseStream.Position;
    }
}
