using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

partial class ProcessOutput
{
    private static void Timers(Output output)
    {
        // Timer output cannot directly activate itself.
        var op = output.Fanin.OfType<OutputPort>().First();
        foreach (var wire in op.Fanin.OfType<Wire>().ToList())
        foreach (var ip in wire.Fanin.OfType<InputPort>().ToList())
        {
            var input = ip.Fanin.OfType<Input>().First();
            if (input.Origin == output.Origin && input.Type == InputID.Timers)
            {
                var newWire = WiringGraph.CopyNode(wire);
                WiringGraph.RemoveEdge(ip, wire);
                WiringGraph.RemoveEdge(newWire, op);
                foreach (var s in newWire.Fanin.ToList())
                    if (s != ip) WiringGraph.RemoveEdge(s, newWire);
                break;
            }
        }
    }
}
