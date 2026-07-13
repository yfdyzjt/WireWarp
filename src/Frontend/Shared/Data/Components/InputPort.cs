namespace WireWarp.Frontend.Shared.Data;

public class InputPort : IConnectable
{
    public int Id { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}
