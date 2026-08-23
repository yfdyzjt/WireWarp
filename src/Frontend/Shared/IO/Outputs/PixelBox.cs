using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.IO;

partial class ProcessOutput
{
    private static void PixelBox(Output output)
    {
        var op = output.Fanin.OfType<OutputPort>().First();
        var o = output.Origin;

        var horizontal = new HashSet<IConnectable>();
        var vertical = new HashSet<IConnectable>();

        foreach (var color in new[] { WireID.Red, WireID.Blue, WireID.Green, WireID.Yellow })
        {
            var tile = Access.Instance.GetTile(o.X, o.Y);
            if (!Detector.HasWire(tile, color)) continue;

            TraceDir((o.X - 1, o.Y), o, horizontal, color);
            TraceDir((o.X + 1, o.Y), o, horizontal, color);
            TraceDir((o.X, o.Y - 1), o, vertical, color);
            TraceDir((o.X, o.Y + 1), o, vertical, color);
        }

        var seen = new HashSet<(int X, int Y)>();
        foreach (var wire in op.Fanin.OfType<Wire>())
        foreach (var sourcePos in wire.Sources)
        {
            if (!seen.Add(sourcePos)) continue;

            var source = WiringGraph.GatePos.TryGetValue(sourcePos, out Gate? gate)
                ? (IConnectable)gate
                : WiringGraph.InputPos[sourcePos];

            if (!horizontal.Contains(source) || !vertical.Contains(source)) continue;

            var newWire = WiringGraph.AddNode(new Wire { Type = wire.Type });
            var newOp = WiringGraph.AddNode(new OutputPort());
            var newSource = source is Input input
                ? input.Fanout.OfType<InputPort>().First()
                : source;

            WiringGraph.AddEdge(newSource, newWire);
            WiringGraph.AddEdge(newWire, newOp);
            WiringGraph.AddEdge(newOp, output);

            newWire.Sources.Add(sourcePos);
            newWire.Drains.Add(o);
        }

        WiringGraph.RemoveNode(op);
    }

    private static void TraceDir(
        (int x, int y) start, (int x, int y) prev,
        HashSet<IConnectable> result,
        WireID color)
    {
        var wire = new Wire { Type = color };
        var visited = new Dictionary<((int, int), WireID), Wire>();

        var founds = Conversion.Trace.TraceWire(
            wire, start, prev, visited);

        result.UnionWith(founds.Select(f => f.component));
    }
}
