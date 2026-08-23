namespace WireWarp.Frontend.Shared.Data;

public static class WiringTemp
{
    public static Dictionary<((int X, int Y) Pos, WireID Type), List<((int x, int y) active, IConnectable component)>> Traces { get; } = [];

    public static void Clean()
    {
        Traces.Clear();
    }
}
