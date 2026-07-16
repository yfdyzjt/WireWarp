using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.ID;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class TraceWires
{
    public static void Execute(WiringGraph graph)
    {
        var wireByTile = new Dictionary<((int x, int y) pos, WireID color), Wire>();

        foreach (var (pos, input) in graph.InputPos)
            TraceSource(pos, input, graph, wireByTile);

        foreach (var (pos, gate) in graph.GatePos)
            TraceSource(pos, gate, graph, wireByTile);
    }

    private static void TraceSource(
        (int x, int y) start,
        IConnectable source,
        WiringGraph graph,
        Dictionary<((int, int), WireID), Wire> wireByTile)
    {
        foreach (var color in new[] { WireID.Red, WireID.Blue, WireID.Green, WireID.Yellow })
        {
            if (!Detector.HasWire(Main.tile(start.x, start.y), color))
                continue;

            if (wireByTile.TryGetValue((start, color), out var oldWire))
            {
                var newSource = source is Input si ? si.Fanout.FirstOrDefault() : source;
                var oldSource = oldWire.Fanin.FirstOrDefault();
                if (oldSource == null || newSource == null || oldSource == newSource)
                    continue;

                var newWire = graph.CopyNode(oldWire);
                WiringGraph.RemoveEdge(oldSource, newWire);
                WiringGraph.AddEdge(newSource, newWire);
                continue;
            }

            var input = source is Input i ? i.Fanout.First() : source;
            var wire = graph.AddWire(color, start.x, start.y);

            WiringGraph.AddEdge(input, wire);
            TraceWire(wire, 0, start, start, graph, wireByTile);
        }
    }

    public static void TraceWire(
        Wire wire,
        int level,
        (int x, int y) start,
        (int x, int y) prevStart,
        WiringGraph graph,
        Dictionary<((int, int), WireID), Wire> wireByTile,
        Action<Wire, (int x, int y), int>? onVisit = null)
    {
        onVisit ??= (w, p, l) => AttachComponents(w, p, graph);

        var queue = new Queue<((int x, int y) cur, (int x, int y) prev, int level)>();
        queue.Enqueue((start, prevStart, level));

        while (queue.Count > 0)
        {
            var (cur, prev, curLevel) = queue.Dequeue();

            if (cur.x < 0 || cur.x >= Main.maxTilesX ||
                cur.y < 0 || cur.y >= Main.maxTilesY)
                continue;

            var tile = Main.tile(cur.x, cur.y);
            if (!Detector.HasWire(tile, wire.Type)) continue;

            var jb = Detector.DetectJunctionBox(tile);
            if (jb == JunctionBoxID.None && wireByTile.ContainsKey((cur, wire.Type)))
                continue;

            wireByTile[(cur, wire.Type)] = wire;
            onVisit(wire, cur, curLevel);

            if (jb != JunctionBoxID.None)
            {
                var next = RouteJunction(cur, prev, jb);
                queue.Enqueue((next, cur, curLevel + 1));
            }
            else
            {
                var prevJb = Detector.DetectJunctionBox(Main.tile(prev.x, prev.y)) != JunctionBoxID.None;

                foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                {
                    var next = (x: cur.x + dx, y: cur.y + dy);
                    if (prevJb && prev == next) continue;
                    queue.Enqueue((next, cur, curLevel + 1));
                }
            }
        }
    }

    private static void AttachComponents(
        Wire wire,
        (int x, int y) pos,
        WiringGraph graph)
    {
        if (graph.LampPos.TryGetValue(pos, out var lamp))
        {
            WiringGraph.AddEdge(wire, lamp);
            return;
        }

        if (graph.GatePos.TryGetValue(pos, out var gate))
        {
            for (var y = gate.Y - 1; ; y--)
            {
                if (graph.LampPos.TryGetValue((gate.X, y), out var gateLamp))
                    WiringGraph.AddEdge(gateLamp, gate);
                else
                    break;
            }
            return;
        }

        if (graph.OutputPos.TryGetValue(pos, out var output))
        {
            var op = output.Fanin.OfType<OutputPort>().First();
            WiringGraph.AddEdge(wire, op);
        }
    }

    private static (int x, int y) RouteJunction(
        (int x, int y) cur,
        (int x, int y) prev,
        JunctionBoxID type)
    {
        return type switch
        {
            JunctionBoxID.UpDown => (
                cur.x + (cur.x - prev.x),
                cur.y + (cur.y - prev.y)),

            JunctionBoxID.UpLeft => (
                cur.x - (cur.y - prev.y),
                cur.y - (cur.x - prev.x)),

            JunctionBoxID.UpRight => (
                cur.x + (cur.y - prev.y),
                cur.y + (cur.x - prev.x)),

            _ => cur
        };
    }
}
