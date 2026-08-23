namespace WireWarp.Frontend.Shared.Data;

public static class IOExtra
{
    private static readonly Dictionary<int, ((int x, int y) source, (int x, int y) target)> _teleporter = [];
    private static readonly Dictionary<int, (List<(int x, int y)> inlets, List<(int x, int y)> outlets)> _pumps = [];
    private static readonly Dictionary<int, WireID> _wireBulb = [];

    public static IReadOnlyDictionary<int, ((int x, int y) source, (int x, int y) target)> Teleporter => _teleporter;
    public static IReadOnlyDictionary<int, (List<(int x, int y)> inlets, List<(int x, int y)> outlets)> Pumps => _pumps;
    public static IReadOnlyDictionary<int, WireID> WireBulb => _wireBulb;

    internal static void SetTeleporter(int portId, ((int x, int y) source, (int x, int y) target) v) =>
        _teleporter[portId] = v;

    internal static void SetPump(int portId, List<(int x, int y)> inlets, List<(int x, int y)> outlets) =>
        _pumps[portId] = (inlets, outlets);

    internal static void SetWireBulb(int portId, WireID type) =>
        _wireBulb[portId] = type;

    public static void Build()
    {
        Clean();

        foreach (var (op, v) in WiringExtra.Teleporter) _teleporter[op.PortId] = v;
        foreach (var (op, v) in WiringExtra.Pumps) _pumps[op.PortId] = v;
        foreach (var (op, v) in WiringExtra.WireBulb) _wireBulb[op.PortId] = v;
    }

    public static void Resolve()
    {
        foreach (var (portId, v) in _teleporter)
        {
            var op = WiringGraph.OutputPorts.FirstOrDefault(p => p.PortId == portId);
            if (op is not null) WiringExtra.Teleporter[op] = v;
            else throw new Exception($"IOExtra.Resolve: Teleporter port {portId} not found in WiringGraph");
        }

        foreach (var (portId, v) in _pumps)
        {
            var op = WiringGraph.OutputPorts.FirstOrDefault(p => p.PortId == portId);
            if (op is not null) WiringExtra.Pumps[op] = v;
            else throw new Exception($"IOExtra.Resolve: Pumps port {portId} not found in WiringGraph");
        }

        foreach (var (portId, v) in _wireBulb)
        {
            var op = WiringGraph.OutputPorts.FirstOrDefault(p => p.PortId == portId);
            if (op is not null) WiringExtra.WireBulb[op] = v;
            else throw new Exception($"IOExtra.Resolve: WireBulb port {portId} not found in WiringGraph");
        }
    }

    public static void Clean()
    {
        _teleporter.Clear();
        _pumps.Clear();
        _wireBulb.Clear();
    }
}
