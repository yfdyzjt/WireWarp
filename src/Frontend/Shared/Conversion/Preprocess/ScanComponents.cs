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
                    inputs[(x, y)] = graph.AddInput(inputType, origin.x, origin.y);
                }

                var outputType = Detector.DetectOutput(tile);
                if (outputType != OutputID.None && Detector.HasWire(tile))
                {
                    var origin = Detector.GetOutputOrigin(outputType, x, y, tile.TileFrameX, tile.TileFrameY);
                    outputs[(x, y)] = graph.AddOutput(outputType, origin.x, origin.y);
                }
            }
        }
    }
}
