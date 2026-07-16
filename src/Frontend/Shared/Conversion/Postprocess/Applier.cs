using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Applier
{
    public static void Execute(WiringGraph graph)
    {
        IO.Processor.Execute(graph);
    }
}
