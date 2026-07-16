using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Split
{
    public static void Execute(WiringGraph graph)
    {
        SplitMultiWireOutputs(graph);
    }

    private static void SplitMultiWireOutputs(WiringGraph graph)
    {
        foreach (var outputPort in graph.OutputPorts.ToList())
        {
            var wires = outputPort.Fanin.ToList();
            if (wires.Count <= 1) continue;

            foreach (var wire in wires)
            {
                var op = (OutputPort)graph.CopyNode(outputPort);
                foreach (var old in op.Fanin.Where(w => w != wire).ToList())
                    WiringGraph.RemoveEdge(old, op);
            }

            graph.RemoveNode(outputPort);
        }
    }
}
