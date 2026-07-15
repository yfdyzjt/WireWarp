using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;

namespace WireWarp.Frontend.Shared.IO;

public class WireBulb : IOutputProcessor
{
    public static readonly WireBulb Instance = new();

    public void Clear() => Data.WireBulb.Clear();

    public void Process(WiringGraph graph, Output output, ITileAccessor world)
    {
        foreach (var op in output.Fanin.OfType<OutputPort>())
        {
            var wire = op.Fanin.OfType<Wire>().First();
            Data.WireBulb.Data[op] = wire.Type;
        }
    }
}
