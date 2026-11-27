using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Trace
{
    public static void Execute()
    {
        var wireByTile = new Dictionary<((int x, int y) pos, WireID color), Wire>();

        var i = 0;
        var total = WiringGraph.InputPos.Keys.Count;
        foreach (var pos in WiringGraph.InputPos.Keys)
        {
            if (i++ % Math.Max(1, total / 100) == 0)
                Access.Instance.Status($"Tracing input {i * 100 / total}%");
            TraceSource(pos, wireByTile);
        }

        i = 0;
        total = WiringGraph.GatePos.Keys.Count;
        foreach (var pos in WiringGraph.GatePos.Keys)
        {
            if (i++ % Math.Max(1, total / 100) == 0)
                Access.Instance.Status($"Tracing gate {i * 100 / total}%");
            TraceSource(pos, wireByTile);
        }
    }

    private static void TraceSource(
        (int x, int y) start,
        Dictionary<((int, int), WireID), Wire> wireByTile)
    {
        foreach (var color in new[] { WireID.Red, WireID.Blue, WireID.Green, WireID.Yellow })
        {
            if (!Detector.HasWire(Access.Instance.GetTile(start.x, start.y), color)) continue;
            if (wireByTile.ContainsKey((start, color))) continue;

            var wire = WiringGraph.AddNode(new Wire { Type = color });
            var founds = TraceWire(wire, start, start, wireByTile);
            ConnectComponents(wire, founds);
        }
    }

    private static void ConnectComponents(
        Wire wire,
        List<((int x, int y) active, IConnectable component)> founds)
    {
        var visited = new HashSet<IConnectable>();
        foreach (var (active, component) in founds)
        {
            if (!visited.Add(component)) continue;

            switch (component)
            {
                case Lamp lamp:
                    WiringGraph.AddEdge(wire, lamp);
                    wire.Drains.Add(active);
                    break;

                case Output output:
                    var op = output.Fanin.OfType<OutputPort>().FirstOrDefault() ?? 
                        WiringGraph.AddNode(new OutputPort());
                    WiringGraph.AddEdge(wire, op);
                    WiringGraph.AddEdge(op, output);
                    wire.Drains.Add(active);
                    break;

                case Gate gate:
                    WiringGraph.AddEdge(gate, wire);
                    ConnectLamps(gate);
                    wire.Sources.Add(active);
                    break;

                case Input input:
                    var ip = input.Fanout.OfType<InputPort>().FirstOrDefault() ?? 
                        WiringGraph.AddNode(new InputPort());
                    WiringGraph.AddEdge(input, ip);
                    WiringGraph.AddEdge(ip, wire);
                    wire.Sources.Add(active);
                    break;
            }
        }
    }

    private static void ConnectLamps(Gate gate)
    {
        for (var y = gate.Origin.Y - 1; ; y--)
        {
            if (WiringGraph.LampPos.TryGetValue((gate.Origin.X, y), out var gateLamp))
                WiringGraph.AddEdge(gateLamp, gate);
            else
                break;
        }
    }

    public static List<((int x, int y) active, IConnectable component)> TraceWire(
        Wire wire,
        (int x, int y) start,
        (int x, int y) prevStart,
        Dictionary<((int, int), WireID), Wire> wireByTile)
    {
        var founds = new List<((int x, int y) active, IConnectable component)>();

        var queue = new Queue<((int x, int y) cur, (int x, int y) prev)>();
        queue.Enqueue((start, prevStart));

        while (queue.Count > 0)
        {
            var (cur, prev) = queue.Dequeue();

            if (cur.x < 0 || cur.x >= Access.Instance.MaxTilesX ||
                cur.y < 0 || cur.y >= Access.Instance.MaxTilesY)
                continue;

            var tile = Access.Instance.GetTile(cur.x, cur.y);
            if (!Detector.HasWire(tile, wire.Type)) continue;

            var jb = Detector.DetectJunctionBox(tile);
            if (jb == JunctionBoxID.None && wireByTile.ContainsKey((cur, wire.Type)))
                continue;

            wireByTile[(cur, wire.Type)] = wire;

            if (WiringGraph.LampPos.TryGetValue(cur, out var lamp))
                founds.Add((cur, lamp));
            if (WiringGraph.GatePos.TryGetValue(cur, out var gate))
                founds.Add((cur, gate));
            if (WiringGraph.InputPos.TryGetValue(cur, out var input))
                founds.Add((cur, input));
            if (WiringGraph.OutputPos.TryGetValue(cur, out var output))
                founds.Add((cur, output));

            if (jb != JunctionBoxID.None)
            {
                var next = RouteJunction(cur, prev, jb);
                queue.Enqueue((next, cur));
            }
            else
            {
                var prevTile = Access.Instance.GetTile(prev.x, prev.y);
                var prevJb = Detector.DetectJunctionBox(prevTile) != JunctionBoxID.None;

                foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                {
                    var next = (x: cur.x + dx, y: cur.y + dy);
                    if (prevJb && prev == next) continue;
                    queue.Enqueue((next, cur));
                }
            }
        }

        return founds;
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
