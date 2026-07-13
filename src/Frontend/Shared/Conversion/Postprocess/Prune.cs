using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion.Postprocess;

public static class Prune
{
    public static void Execute(WiringGraph graph)
    {
        bool changed;
        do
        {
            changed = false;
            changed |= PruneWhere(graph, graph.Wires);
            changed |= PruneWhere(graph, graph.Gates);
            changed |= PruneWhere(graph, graph.Lamps);
            changed |= PruneWhere(graph, graph.Inputs);
            changed |= PruneWhere(graph, graph.Outputs);
            changed |= PruneWhere(graph, graph.InputPorts);
            changed |= PruneWhere(graph, graph.OutputPorts);
        }
        while (changed);
    }

    private static bool PruneWhere<T>(WiringGraph graph, IReadOnlyList<T> nodes) where T : IConnectable
    {
        var removed = false;
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            if (IsDead(nodes[i]))
            {
                graph.RemoveNode(nodes[i]);
                removed = true;
            }
        }
        return removed;
    }

    private static bool IsDead(IConnectable node) => node switch
    {
        Wire or Gate or InputPort or OutputPort =>
            node.Fanin.Count == 0 || node.Fanout.Count == 0,
        Lamp or Input => node.Fanout.Count == 0,
        Output => node.Fanin.Count == 0,
        _ => false,
    };
}
