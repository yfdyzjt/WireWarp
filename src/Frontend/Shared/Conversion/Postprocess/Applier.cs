using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Applier
{
    public static void Execute(WiringGraph graph, ITileAccessor world)
    {
        IO.Processor.Execute(graph, world);
    }
}
