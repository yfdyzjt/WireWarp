using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

public class WireBulb : IOutputProcessor
{
    public static readonly WireBulb Instance = new();

    public void Process(WiringGraph graph, Output output)
    {
        foreach (var op in output.Fanin.OfType<OutputPort>())
        {
            var wire = op.Fanin.OfType<Wire>().First();
            graph.ExtraData.WireBulb[op] = wire.Type;
        }
    }
}
