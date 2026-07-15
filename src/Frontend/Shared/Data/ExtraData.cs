using WireWarp.Frontend.Shared.ID;

namespace WireWarp.Frontend.Shared.Data;

public class ExtraData
{
    public Dictionary<OutputPort, (Output source, Output target)> Teleporter { get; } = [];
    public Dictionary<OutputPort, (List<Output> inlets, List<Output> outlets)> Pumps { get; } = [];
    public Dictionary<OutputPort, WireID> WireBulb { get; } = [];
}
