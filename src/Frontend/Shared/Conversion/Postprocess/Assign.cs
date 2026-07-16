using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Assign
{
    public static void Execute(WiringGraph graph)
    {
        AssignIds(graph);
    }

    private static void AssignIds(WiringGraph graph)
    {
        for (var i = 0; i < graph.Wires.Count; i++) graph.Wires[i].Id = i;
        for (var i = 0; i < graph.Gates.Count; i++) graph.Gates[i].Id = i;
        for (var i = 0; i < graph.Lamps.Count; i++) graph.Lamps[i].Id = i;
        for (var i = 0; i < graph.Inputs.Count; i++) graph.Inputs[i].Id = i;
        for (var i = 0; i < graph.InputPorts.Count; i++) graph.InputPorts[i].Id = i;
        for (var i = 0; i < graph.Outputs.Count; i++) graph.Outputs[i].Id = i;
        for (var i = 0; i < graph.OutputPorts.Count; i++) graph.OutputPorts[i].Id = i;
    }
}
