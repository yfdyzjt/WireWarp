using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Converter
{
    public static WiringGraph Convert()
    {
        var graph = new WiringGraph();

        // preprocess
        ScanComponents.Execute(graph);
        TraceWires.Execute(graph);

        // postprocess
        Prune.Execute(graph);
        Normalize.Execute(graph);
        Prune.Execute(graph);
        Split.Execute(graph);
        Applier.Execute(graph);
        Prune.Execute(graph);
        Assign.Execute(graph);

        return graph;
    }
}
