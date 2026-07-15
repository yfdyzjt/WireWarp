using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.ID;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.IO;

public class Pumps : IOutputProcessor
{
    public static readonly Pumps Instance = new();
    
    public void Process(WiringGraph graph, Output output, ITileAccessor world)
    {
        foreach (var op in output.Fanin.OfType<OutputPort>().ToList())
        {
            var wire = op.Fanin.OfType<Wire>().First();

            var (inlets, outlets) = Analyze(wire, graph, world);

            if (inlets != null && inlets[0] == output)
                graph.ExtraData.Pumps[op] = (inlets, outlets!);
            else
                graph.RemoveNode(op);
        }
    }

    private static (List<Output>? inlets, List<Output>? outlets) Analyze(
        Wire wire, WiringGraph graph, ITileAccessor world)
    {
        var inlets = new List<Output>();
        var outlets = new List<Output>();
        var visited = new Dictionary<((int, int), WireID), Wire>();

        Conversion.TraceWires.TraceWire(
            wire, 0, (wire.X, wire.Y), (wire.X, wire.Y),
            graph, world, visited,
            (w, pos, _) =>
            {
                if (!graph.OutputPos.TryGetValue(pos, out var o)) return;
                if (o.Type != OutputID.Pumps) return;

                var tileType = world.GetTile(pos.x, pos.y).TileType;
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
