using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Data;

public static class WireBulb
{
    public static Dictionary<OutputPort, WireID> Data { get; } = [];

    public static void Clear() => Data.Clear();
}
