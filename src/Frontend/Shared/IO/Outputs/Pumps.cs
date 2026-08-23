using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.IO;

partial class ProcessOutput
{
    private static void Pumps(Output output)
    {
        var op = output.Fanin.OfType<OutputPort>().First();

        var seen = new HashSet<(int X, int Y)>();
        foreach (var wire in op.Fanin.OfType<Wire>())
        foreach (var sourcePos in wire.Sources)
        {
            if (!seen.Add(sourcePos)) continue;

            var key = (sourcePos, wire.Type);
            if (!WiringTemp.Traces.TryGetValue(key, out var founds))
            {
                var wireMap = new Dictionary<((int, int), WireID), Wire>();
                founds = Conversion.Trace.TraceWire(
                    wire, sourcePos, sourcePos, wireMap);
                WiringTemp.Traces[key] = founds;
            }

            var pumps = founds
                .Where(f => f.component is Output { Type: OutputID.Pumps })
                .ToList();

            var inlets = new List<(int x, int y)>();
            var outlets = new List<(int x, int y)>();
            
            var visited = new HashSet<Output>();
            foreach (var (active, component) in pumps)
            {
                var pump = (Output)component;
                if (!visited.Add(pump)) continue;

                var tileType = Access.Instance.GetTile(active.x, active.y).type;
                if (tileType == TileID.InletPump)
                    inlets.Add(active);
                else if (tileType == TileID.OutletPump)
                    outlets.Add(active);
            }

            if (inlets.Count == 0 || outlets.Count == 0 || 
                WiringGraph.OutputPos[inlets[0]] != output) continue;

            var source = WiringGraph.GatePos.TryGetValue(sourcePos, out Gate? gate)
                ? (IConnectable)gate
                : WiringGraph.InputPos[sourcePos].Fanout.OfType<InputPort>().First();

            var newWire = WiringGraph.AddNode(new Wire { Type = wire.Type });
            var newOp = WiringGraph.AddNode(new OutputPort());

            WiringGraph.AddEdge(source, newWire);
            WiringGraph.AddEdge(newWire, newOp);
            WiringGraph.AddEdge(newOp, output);

            newWire.Sources.Add(sourcePos);
            newWire.Drains.Add(inlets[0]);

            WiringExtra.Pumps[newOp] = (inlets, outlets);
        }

        WiringGraph.RemoveNode(op);
    }
}
