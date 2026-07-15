using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.ID;

namespace WireWarp.Frontend.Shared.IO;

public class Teleporter : IOutputProcessor
{
    public static readonly Teleporter Instance = new();
    
    public void Process(WiringGraph graph, Output output, ITileAccessor world)
    {
        foreach (var op in output.Fanin.OfType<OutputPort>().ToList())
        {
            var wire = op.Fanin.OfType<Wire>().First();

            var (minPort, source, target) = Analyze(wire, graph, world);

            if (minPort != null && source != null && target != null && minPort == op)
                graph.ExtraData.Teleporter[op] = (source, target);
            else
                graph.RemoveNode(op);
        }
    }

    private static (OutputPort? minPort, Output? source, Output? target) Analyze(
        Wire wire, WiringGraph graph, ITileAccessor world)
    {
        var visited = new Dictionary<((int, int), WireID), Wire>();
        var found = new List<(Output output, OutputPort port, int level)>();

        Conversion.TraceWires.TraceWire(
            wire, 0, (wire.X, wire.Y), (wire.X, wire.Y),
            graph, world, visited,
            (w, pos, level) =>
            {
                if (!graph.OutputPos.TryGetValue(pos, out var foundOutput)) return;
                if (foundOutput.Type != OutputID.Teleporter) return;

                var foundPort = w.Fanout
                    .OfType<OutputPort>()
                    .FirstOrDefault(p => p.Fanout.Contains(foundOutput));
                if (foundPort != null)
                    found.Add((foundOutput, foundPort, level));
            });

        if (found.Count < 2) return (null, null, null);

        var min = found.MinBy(f => f.level);
        var max = found.MaxBy(f => f.level);

        if (min.port == max.port) return (null, null, null);

        return (min.port, min.output, max.output);
    }
}
