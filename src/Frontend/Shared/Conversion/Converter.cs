using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Converter
{
    public static WiringGraph Convert(ITileAccessor world)
    {
        var graph = new WiringGraph();

        // preprocess
        ScanComponents.Execute(graph, world);
        TraceWires.Execute(graph, world);

        // postprocess
        Prune.Execute(graph);
        Normalize.Execute(graph);
        Prune.Execute(graph);
        Split.Execute(graph);
        Applier.Execute(graph, world);
        Prune.Execute(graph);
        Assign.Execute(graph);

        return graph;
    }
}
