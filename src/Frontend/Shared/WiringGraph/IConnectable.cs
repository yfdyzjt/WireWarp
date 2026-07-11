namespace WireWarp.Frontend.Shared.WiringGraph;

public interface IConnectable
{
    int Id { get; set; }
    HashSet<IConnectable> Fanin { get; }
    HashSet<IConnectable> Fanout { get; }
}
