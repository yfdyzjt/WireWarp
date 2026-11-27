using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Normalize
{
    public static void Execute()
    {
        Access.Instance.Status("Normalizing wiring...");
        NormalizeFaultGates();
    }

    private static void NormalizeFaultGates()
    {
        foreach (var gate in WiringGraph.Gates.Where(g => g.Type == GateID.Fault))
        {
            var lamps = gate.Fanin.OfType<Lamp>()
                .OrderByDescending(l => l.Origin.Y)
                .ToList();

            var faultLamp = lamps.FirstOrDefault(l => l.Type == LampID.Fault);
            if (faultLamp == null) continue;
            foreach (var lamp in lamps.Where(l => l.Origin.Y < faultLamp.Origin.Y))
            {
                if (lamp.Type == LampID.Fault)
                {
                    foreach (var wire in lamp.Fanin)
                        WiringGraph.AddEdge(wire, faultLamp);
                }

                WiringGraph.RemoveNode(lamp);
            }
        }
    }
}
