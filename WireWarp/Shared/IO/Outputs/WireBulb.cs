using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

partial class ProcessOutput
{
    private static void WireBulb(Output output)
    {
        var oldOp = output.Fanin.OfType<OutputPort>().First();
        
        foreach (var wire in oldOp.Fanin.OfType<Wire>().ToList())
        {
            var newOp = WiringGraph.AddNode(new OutputPort());
            WiringGraph.AddEdge(wire, newOp);
            WiringGraph.AddEdge(newOp, output);
        }

        WiringGraph.RemoveNode(oldOp);

        foreach (var op in output.Fanin.OfType<OutputPort>())
        {
            var wire = op.Fanin.OfType<Wire>().First();
            WiringExtra.WireBulb[op] = wire.Type;
        }
    }
}
