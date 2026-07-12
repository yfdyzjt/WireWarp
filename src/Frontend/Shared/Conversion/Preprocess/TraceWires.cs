using WireWarp.Frontend.Shared.Graph;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Conversion.Preprocess;

public static class TraceWires
{
    public static void Execute(
        ITileAccessor world,
        WiringGraph graph,
        Dictionary<(int x, int y), Input> inputs,
        Dictionary<(int x, int y), Gate> gates,
        Dictionary<(int x, int y), Lamp> lamps,
        Dictionary<(int x, int y), Output> outputs)
    {
        var visited = new HashSet<((int x, int y) pos, WireID color)>();

        foreach (var (pos, _) in inputs)
            TraceSource(pos, graph, world, visited, inputs, gates, lamps, outputs);

        foreach (var (pos, _) in gates)
            TraceSource(pos, graph, world, visited, inputs, gates, lamps, outputs);
    }

    private static void TraceSource(
        (int x, int y) start,
        WiringGraph graph,
        ITileAccessor world,
        HashSet<((int, int), WireID)> visited,
        Dictionary<(int x, int y), Input> inputs,
        Dictionary<(int x, int y), Gate> gates,
        Dictionary<(int x, int y), Lamp> lamps,
        Dictionary<(int x, int y), Output> outputs)
    {
        foreach (var color in Enum.GetValues<WireID>())
        {
            if (!Detector.HasWire(world.GetTile(start.x, start.y), color))
                continue;

            if (visited.Contains((start, color)))
                continue;

            var wire = graph.AddWire(color);
            TraceWire(wire, 0, start, start, graph, world, visited, inputs, gates, lamps, outputs);
        }
    }

    public static void TraceWire(
        Wire wire,
        int level,
        (int x, int y) start,
        (int x, int y) prevStart,
        WiringGraph graph,
        ITileAccessor world,
        HashSet<((int, int), WireID)> visited,
        Dictionary<(int x, int y), Input> inputs,
        Dictionary<(int x, int y), Gate> gates,
        Dictionary<(int x, int y), Lamp> lamps,
        Dictionary<(int x, int y), Output> outputs,
        Action<Wire, (int x, int y), int>? onVisit = null)
    {
        onVisit ??= (w, p, l) => AttachComponents(w, p, graph, inputs, gates, lamps, outputs);

        var queue = new Queue<((int x, int y) cur, (int x, int y) prev, int level)>();
        queue.Enqueue((start, prevStart, level));

        while (queue.Count > 0)
        {
            var (cur, prev, curLevel) = queue.Dequeue();

            if (cur.x < 0 || cur.x >= world.GetWorldWidth() ||
                cur.y < 0 || cur.y >= world.GetWorldHeight())
                continue;

            var tile = world.GetTile(cur.x, cur.y);
            if (!Detector.HasWire(tile, wire.Type)) continue;

            var jb = Detector.DetectJunctionBox(tile);
            if (jb == JunctionBoxID.None && visited.Contains((cur, wire.Type)))
                continue;

            visited.Add((cur, wire.Type));
            onVisit(wire, cur, curLevel);

            if (jb != JunctionBoxID.None)
            {
                var next = RouteJunction(cur, prev, jb);
                queue.Enqueue((next, cur, curLevel + 1));
            }
            else
            {
                var prevJb = Detector.DetectJunctionBox(world.GetTile(prev.x, prev.y)) != JunctionBoxID.None;

                foreach (var (dx, dy) in new [] { (1, 0), (0, 1), (-1, 0), (0, -1) })
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
        WiringGraph graph,
        Dictionary<(int x, int y), Input> inputs,
        Dictionary<(int x, int y), Gate> gates,
        Dictionary<(int x, int y), Lamp> lamps,
        Dictionary<(int x, int y), Output> outputs)
    {
        if (lamps.TryGetValue(pos, out var lamp))
        {
            WiringGraph.AddEdge(wire, lamp);
            return;
        }

        if (gates.TryGetValue(pos, out var gate))
        {
            var gateLamps = FindLamps(gate, lamps);
            WiringGraph.AddEdge(gate, wire);
            foreach (var l in gateLamps)
                WiringGraph.AddEdge(l, gate);
            return;
        }

        if (inputs.TryGetValue(pos, out var input))
        {
            var ip = input.Fanout.OfType<InputPort>().FirstOrDefault()
                     ?? graph.AddInputPort();
            WiringGraph.AddEdge(input, ip);
            WiringGraph.AddEdge(ip, wire);
        }

        if (outputs.TryGetValue(pos, out var output))
        {
            var op = output.Fanout.OfType<OutputPort>().FirstOrDefault()
                     ?? graph.AddOutputPort();
            WiringGraph.AddEdge(output, op);
            WiringGraph.AddEdge(op, wire);
        }
    }
    private static List<Lamp> FindLamps(Gate gate, Dictionary<(int x, int y), Lamp> lamps)
    {
        var result = new List<Lamp>();

        for (var y = gate.Y - 1; ; y--)
        {
            if (lamps.TryGetValue((gate.X, y), out var lamp))
                result.Add(lamp);
            else
                break;
        }

        return result;
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
