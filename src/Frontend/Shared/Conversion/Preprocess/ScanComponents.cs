using WireWarp.Frontend.Shared.Graph;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Conversion.Preprocess;

public static class ScanComponents
{
    public static void Execute(
        ITileAccessor world,
        WiringGraph graph,
        out Dictionary<(int x, int y), Input> inputs,
        out Dictionary<(int x, int y), Gate> gates,
        out Dictionary<(int x, int y), Lamp> lamps,
        out Dictionary<(int x, int y), Output> outputs)
    {
        inputs = [];
        gates = [];
        lamps = [];
        outputs = [];

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
                    gates[(x, y)] = graph.AddGate(gateType, x, y);
                    continue;
                }

                var lampType = Detector.DetectLamp(tile);
                if (lampType != LampID.None)
                {
                    lamps[(x, y)] = graph.AddLamp(lampType, x, y);
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
                    inputs[(x, y)] = input;
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
                    outputs[(x, y)] = output;
                }
            }
        }
    }
    
    private static bool InRange(int x, int y, (int x, int y) origin, (int x, int y) size)
        => x >= origin.x && x < origin.x + size.x
        && y >= origin.y && y < origin.y + size.y;
}
