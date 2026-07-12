namespace WireWarp.Frontend.Shared.Graph;

public interface IConnectable
{
    int Id { get; set; }
    HashSet<IConnectable> Fanin { get; }
    HashSet<IConnectable> Fanout { get; }
}
