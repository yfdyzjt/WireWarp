namespace WireWarp.Frontend.Shared.Data;

public static class WiringExtra
{
    public static Dictionary<OutputPort, ((int x, int y) source, (int x, int y) target)> Teleporter { get; } = [];
    public static Dictionary<OutputPort, (List<(int x, int y)> inlets, List<(int x, int y)> outlets)> Pumps { get; } = [];
    public static Dictionary<OutputPort, WireID> WireBulb { get; } = [];

    public static void Clean()
    {
        Teleporter.Clear();
        Pumps.Clear();
        WireBulb.Clear();
    }
}
