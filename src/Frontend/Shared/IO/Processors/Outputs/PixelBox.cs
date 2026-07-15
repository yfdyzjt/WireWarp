using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.IO;

public class PixelBox : IOutputProcessor
{
    public static readonly PixelBox Instance = new();

    public void Process(WiringGraph graph, Output output, ITileAccessor world)
    {
        var sources = new HashSet<IConnectable>();
        foreach (var op in output.Fanin.OfType<OutputPort>())
            sources.Add(op.Fanin.OfType<Wire>().First().Fanin.First());

        var horizontal = new HashSet<IConnectable>();
        var vertical = new HashSet<IConnectable>();

        foreach (var color in new[] { WireID.Red, WireID.Blue, WireID.Green, WireID.Yellow })
        {
            if (!Conversion.Detector.HasWire(world.GetTile(output.X, output.Y), color))
                continue;

            TraceDir((output.X - 1, output.Y), (output.X, output.Y), sources, horizontal, color, graph, world);
            TraceDir((output.X + 1, output.Y), (output.X, output.Y), sources, horizontal, color, graph, world);
            TraceDir((output.X, output.Y - 1), (output.X, output.Y), sources, vertical, color, graph, world);
            TraceDir((output.X, output.Y + 1), (output.X, output.Y), sources, vertical, color, graph, world);
        }

        horizontal.IntersectWith(vertical);
        var keep = horizontal;

        foreach (var op in output.Fanin.OfType<OutputPort>().ToList())
        {
            var source = op.Fanin.OfType<Wire>().First().Fanin.First();
            if (!keep.Contains(source))
                graph.RemoveNode(op);
        }
    }

    private static void TraceDir(
        (int x, int y) s, (int x, int y) c,
        HashSet<IConnectable> sources,
        HashSet<IConnectable> result,
        WireID color,
        WiringGraph graph,
        ITileAccessor world)
    {
        var visited = new Dictionary<((int, int), WireID), Wire>();
        var tempWire = new Wire { Type = color };

        Conversion.TraceWires.TraceWire(
            tempWire, 0, s, c,
            graph, world, visited,
            (w, pos, _) =>
            {
                if (graph.GatePos.TryGetValue(pos, out var gate) && sources.Contains(gate))
                    result.Add(gate);
                else if (graph.InputPos.TryGetValue(pos, out var input))
                {
                    var ip = input.Fanout.OfType<InputPort>().FirstOrDefault();
                    if (ip != null && sources.Contains(ip))
                        result.Add(ip);
                }
            });
    }
}
