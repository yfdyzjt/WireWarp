using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

internal class Timers : IOutputProcessor
{
    public static readonly Timers Instance = new();

    public void Process(WiringGraph graph, Output output)
    {
        // Timer output cannot directly activate itself.
        foreach (var op in output.Fanin.OfType<OutputPort>())
        foreach (var wire in op.Fanin.OfType<Wire>().ToList())
        {
            if (wire.Fanin.OfType<InputPort>()
                .Any(ip => ip.Fanin.OfType<Input>()
                .Any(i => i.X == output.X && i.Y == output.Y)))
            {
                WiringGraph.RemoveEdge(wire, op);
            }
        }
    }
}
