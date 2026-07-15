using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.ID;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Normalize
{
    public static void Execute(WiringGraph graph)
    {
        NormalizeFaultGates(graph);
    }

    private static void NormalizeFaultGates(WiringGraph graph)
    {
        foreach (var gate in graph.Gates.Where(g => g.Type == GateID.Fault))
        {
            var lamps = gate.Fanin.OfType<Lamp>()
                .OrderByDescending(l => l.Y)
                .ToList();

            var faultLamp = lamps.FirstOrDefault(l => l.Type == LampID.Fault);

            // TODO: Fault gate without a fault lamp is an error in the world.
            // Should be reported via diagnostics once error handling is implemented.
            if (faultLamp == null) continue;

            foreach (var lamp in lamps.Where(l => l.Y < faultLamp.Y))
            {
                if (lamp.Type == LampID.Fault)
                    foreach (var wire in lamp.Fanin)
                        WiringGraph.AddEdge(wire, faultLamp);

                graph.RemoveNode(lamp);
            }
        }
    }
}
