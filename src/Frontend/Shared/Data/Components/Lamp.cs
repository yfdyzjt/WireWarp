using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Data;

public class Lamp : IConnectable
{
    public int Id { get; set; }
    public LampID Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}
