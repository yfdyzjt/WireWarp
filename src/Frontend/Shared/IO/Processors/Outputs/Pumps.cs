using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.IO;

public class Pumps : IOutputProcessor
{
    public static readonly Pumps Instance = new();

    private readonly Dictionary<Wire, (List<Output> inlets, List<Output> outlets)> _pairs = [];

    public void Clear()
    {
        Data.Pumps.Clear();
        _pairs.Clear();
    }

    public void Process(WiringGraph graph, Output output, ITileAccessor world)
    {
        foreach (var op in output.Fanin.OfType<OutputPort>().ToList())
        {
            var wire = op.Fanin.OfType<Wire>().First();

            if (!_pairs.ContainsKey(wire))
                Analyze(wire, graph, world);

            if (_pairs.TryGetValue(wire, out var pair) && pair.inlets[0] == output)
                Data.Pumps.Data[op] = pair;
            else
                graph.RemoveNode(op);
        }
    }

    private void Analyze(Wire wire, WiringGraph graph, ITileAccessor world)
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

        if (inlets.Count > 0 && outlets.Count > 0)
            _pairs[wire] = (inlets, outlets);
    }
}
