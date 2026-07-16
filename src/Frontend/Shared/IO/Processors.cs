using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.ID;

namespace WireWarp.Frontend.Shared.IO;

public static class Processor
{
    private static readonly IOutputProcessor?[] _outputs =
        new IOutputProcessor?[Enum.GetValues<OutputID>().Length];

    static Processor()
    {
        _outputs[(int)OutputID.Teleporter] = Teleporter.Instance;
        _outputs[(int)OutputID.Pumps]      = Pumps.Instance;
        _outputs[(int)OutputID.PixelBox]   = PixelBox.Instance;
        _outputs[(int)OutputID.Timers]     = Timers.Instance;
        _outputs[(int)OutputID.WireBulb]   = WireBulb.Instance;
    }

    internal static void Execute(WiringGraph graph)
    {
        foreach (var output in graph.Outputs)
            _outputs[(int)output.Type]?.Process(graph, output);
    }
}
