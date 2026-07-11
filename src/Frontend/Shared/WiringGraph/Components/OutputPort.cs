namespace WireWarp.Frontend.Shared.WiringGraph;

public class OutputPort : IConnectable
{
    public int Id { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}
