using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Prune
{
    public static void Execute()
    {
        Access.Instance.Status("Pruning wiring...");
        bool changed;
        do
        {
            changed = false;
            changed |= PruneWhere(WiringGraph.Wires);
            changed |= PruneWhere(WiringGraph.Gates);
            changed |= PruneWhere(WiringGraph.Lamps);
            changed |= PruneWhere(WiringGraph.Inputs);
            changed |= PruneWhere(WiringGraph.Outputs);
            changed |= PruneWhere(WiringGraph.InputPorts);
            changed |= PruneWhere(WiringGraph.OutputPorts);
        }
        while (changed);
    }

    private static bool PruneWhere<T>(IReadOnlySet<T> nodes) where T : IConnectable
    {
        var dead = new List<IConnectable>();
        foreach (var node in nodes)
            if (IsDead(node)) dead.Add(node);

        foreach (var node in dead)
        {
            Report.AddPruned(node is Gate { Type: GateID.Fault } ? "FaultGate" : node.GetType().Name);
            WiringGraph.RemoveNode(node);
        }

        return dead.Count > 0;
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
