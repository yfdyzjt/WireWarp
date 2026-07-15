using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Conversion;

public static class ScanComponents
{
    public static void Execute(ITileAccessor world, WiringGraph graph)
    {
        Scan(world, graph);
        CreatePorts(graph);
    }

    private static void Scan(ITileAccessor world, WiringGraph graph)
    {
        var inputByOrigin = new Dictionary<(int x, int y, InputID type), Input>();
        var outputByOrigin = new Dictionary<(int x, int y, OutputID type), Output>();

        var w = world.GetWorldWidth();
        var h = world.GetWorldHeight();

        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                var tile = world.GetTile(x, y);
                if (!tile.HasTile) continue;

                var gateType = Detector.DetectGate(tile);
                if (gateType != GateID.None)
                {
                    graph.GatePos[(x, y)] = graph.AddGate(gateType, x, y);
                    continue;
                }

                var lampType = Detector.DetectLamp(tile);
                if (lampType != LampID.None)
                {
                    graph.LampPos[(x, y)] = graph.AddLamp(lampType, x, y);
                    continue;
                }

                var inputType = Detector.DetectInput(tile);
                if (inputType != InputID.None && Detector.HasWire(tile))
                {
                    var origin = Detector.GetInputOrigin(inputType, x, y, tile.TileFrameX, tile.TileFrameY);
                    var size = Detector.GetInputSize(inputType);
                    var inRange = InRange(x, y, origin, size);
                    var key = (origin.x, origin.y, inputType);

                    var input = inRange && inputByOrigin.TryGetValue(key, out var merged)
                        ? merged
                        : graph.AddInput(inputType, origin.x, origin.y);

                    if (inRange) inputByOrigin[key] = input;
                    graph.InputPos[(x, y)] = input;
                }

                var outputType = Detector.DetectOutput(tile);
                if (outputType != OutputID.None && Detector.HasWire(tile))
                {
                    var origin = Detector.GetOutputOrigin(outputType, x, y, tile.TileFrameX, tile.TileFrameY);
                    var size = Detector.GetOutputSize(outputType);
                    var inRange = InRange(x, y, origin, size);
                    var key = (origin.x, origin.y, outputType);

                    var output = inRange && outputByOrigin.TryGetValue(key, out var merged)
                        ? merged
                        : graph.AddOutput(outputType, origin.x, origin.y);

                    if (inRange) outputByOrigin[key] = output;
                    graph.OutputPos[(x, y)] = output;
                }
            }
        }
    }

    private static void CreatePorts(WiringGraph graph)
    {
        foreach (var input in graph.Inputs)
            if (!input.Fanout.OfType<InputPort>().Any())
                WiringGraph.AddEdge(input, graph.AddInputPort());

        foreach (var output in graph.Outputs)
            if (!output.Fanin.OfType<OutputPort>().Any())
                WiringGraph.AddEdge(graph.AddOutputPort(), output);
    }

    private static bool InRange(int x, int y, (int x, int y) origin, (int x, int y) size)
        => x >= origin.x && x < origin.x + size.x
        && y >= origin.y && y < origin.y + size.y;
}
