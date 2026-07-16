using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.ID;
using WireWarp.Frontend.Shared.Terraria;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.IO;

internal class Pumps : IOutputProcessor
{
    public static readonly Pumps Instance = new();

    public void Process(WiringGraph graph, Output output)
    {
        foreach (var op in output.Fanin.OfType<OutputPort>().ToList())
        {
            var wire = op.Fanin.OfType<Wire>().First();

            var (inlets, outlets) = Analyze(wire, graph);

            if (inlets != null && inlets[0] == output)
                graph.ExtraData.Pumps[op] = (inlets, outlets!);
            else
                graph.RemoveNode(op);
        }
    }

    private static (List<Output>? inlets, List<Output>? outlets) Analyze(
        Wire wire, WiringGraph graph)
    {
        var inlets = new List<Output>();
        var outlets = new List<Output>();
        var visited = new Dictionary<((int, int), WireID), Wire>();

        Conversion.TraceWires.TraceWire(
            wire, 0, (wire.X, wire.Y), (wire.X, wire.Y),
            graph, visited,
            (w, pos, _) =>
            {
                if (!graph.OutputPos.TryGetValue(pos, out var o)) return;
                if (o.Type != OutputID.Pumps) return;

                var tileType = Main.tile(pos.x, pos.y).type;
                if (tileType == TileID.InletPump && !inlets.Contains(o))
                    inlets.Add(o);
                else if (tileType == TileID.OutletPump && !outlets.Contains(o))
                    outlets.Add(o);
            });

        if (inlets.Count == 0 || outlets.Count == 0)
            return (null, null);

        return (inlets, outlets);
    }
}
