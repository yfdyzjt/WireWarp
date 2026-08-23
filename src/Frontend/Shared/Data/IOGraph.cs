namespace WireWarp.Frontend.Shared.Data;

public static class IOGraph
{
    private static readonly byte[] _hash = new byte[32];

    public static ReadOnlyMemory<byte> Hash => _hash;

    internal static void SetHash(byte[] hash) => hash.CopyTo(_hash, 0);

    private static readonly Dictionary<(int x, int y), (int portId, InputID type)> _inputs = [];
    private static readonly Dictionary<int, ((int x, int y) pos, OutputID type)> _outputs = [];

    private static readonly Dictionary<int, (int x, int y)> _gatePos = [];
    private static readonly Dictionary<int, (int x, int y)> _lampPos = [];

    public static IReadOnlyDictionary<(int x, int y), (int portId, InputID type)> Inputs => _inputs;
    public static IReadOnlyDictionary<int, ((int x, int y) pos, OutputID type)> Outputs => _outputs;

    public static IReadOnlyDictionary<int, (int x, int y)> GatePos => _gatePos;
    public static IReadOnlyDictionary<int, (int x, int y)> LampPos => _lampPos;

    internal static void SetInput((int x, int y) pos, int portId, InputID type) =>
        _inputs[pos] = (portId, type);

    internal static void SetOutput(int portId, (int x, int y) pos, OutputID type) =>
        _outputs[portId] = (pos, type);

    internal static void SetGatePos(int id, (int x, int y) pos) =>
        _gatePos[id] = pos;

    internal static void SetLampPos(int id, (int x, int y) pos) =>
        _lampPos[id] = pos;

    public static void Build()
    {
        Clean();

        foreach (var (pos, input) in WiringGraph.InputPos)
        {
            var ip = input.Fanout.OfType<InputPort>().FirstOrDefault();
            if (ip != null) _inputs[pos] = (ip.PortId, input.Type);
        }

        foreach (var op in WiringGraph.OutputPorts)
        {
            var output = op.Fanout.OfType<Output>().First();
            var wire = op.Fanin.OfType<Wire>().First();
            var pos = wire.Drains.First(d => WiringGraph.OutputPos.TryGetValue(d, out var o) && o == output);
            _outputs[op.PortId] = (pos, output.Type);
        }

        foreach (var lamp in WiringGraph.Lamps)
            _lampPos[lamp.Id] = lamp.Origin;

        foreach (var gate in WiringGraph.Gates)
            _gatePos[gate.Id] = gate.Origin;

        SetHash(WiringGraph.Hash.Span.ToArray());

        IOExtra.Build();
    }

    public static void Resolve()
    {
        var components = WiringGraph.Components;

        foreach (var (id, pos) in _lampPos)
        {
            if (components.TryGetValue(id, out var node) && node is Lamp lamp)
            {
                lamp.Origin = pos;
                WiringGraph.LampPos[pos] = lamp;
            }
            else throw new Exception($"IOGraph.Resolve: Lamp {id} not found in WiringGraph");
        }

        foreach (var (id, pos) in _gatePos)
        {
            if (components.TryGetValue(id, out var node) && node is Gate gate)
            {
                gate.Origin = pos;
                WiringGraph.GatePos[pos] = gate;
            }
            else throw new Exception($"IOGraph.Resolve: Gate {id} not found in WiringGraph");
        }

        // Inputs and outputs are resolved by world load, not reconstructed here.

        IOExtra.Resolve();
    }

    public static void Clean()
    {
        Array.Clear(_hash);

        _inputs.Clear();
        _outputs.Clear();
        _gatePos.Clear();
        _lampPos.Clear();

        IOExtra.Clean();
    }
}
