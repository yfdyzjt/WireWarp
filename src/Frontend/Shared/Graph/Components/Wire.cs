using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Graph;

public class Wire : IConnectable
{
    public int Id { get; set; }
    public WireID Type { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}
