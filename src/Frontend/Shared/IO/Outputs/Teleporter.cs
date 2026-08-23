using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

partial class ProcessOutput
{
    private static void Teleporter(Output output)
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

            var teleporters = founds
                .Where(f => f.component is Output { Type: OutputID.Teleporter })
                .ToList();

            var origin = teleporters[0];
            var target = teleporters[^1];

            if (origin == target || origin.component != output) continue;

            var source = WiringGraph.GatePos.TryGetValue(sourcePos, out Gate? gate)
                ? (IConnectable)gate
                : WiringGraph.InputPos[sourcePos].Fanout.OfType<InputPort>().First();

            var newWire = WiringGraph.AddNode(new Wire { Type = wire.Type });
            var newOp = WiringGraph.AddNode(new OutputPort());

            WiringGraph.AddEdge(source, newWire);
            WiringGraph.AddEdge(newWire, newOp);
            WiringGraph.AddEdge(newOp, output);

            newWire.Sources.Add(sourcePos);
            newWire.Drains.Add(origin.active);

            WiringExtra.Teleporter[newOp] = (origin.active, target.active);
        }

        WiringGraph.RemoveNode(op);
    }
}
