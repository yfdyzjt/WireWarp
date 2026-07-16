using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Applier
{
    public static void Execute(WiringGraph graph)
    {
        IO.Processor.Execute(graph);
    }
}
