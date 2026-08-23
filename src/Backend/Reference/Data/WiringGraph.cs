namespace WireWarp.Backend.Reference.Data;

public static class WiringGraph
{
    private static readonly byte[] _hash = new byte[32];

    internal static ReadOnlySpan<byte> Hash => _hash;

    internal static void SetHash(ReadOnlySpan<byte> hash) => hash.CopyTo(_hash);

    private static readonly List<InputPort> _inputPorts = [];
    private static readonly List<OutputPort> _outputPorts = [];
    private static readonly List<Lamp> _lamps = [];
    private static readonly List<Gate> _gates = [];
    private static readonly List<Wire> _wires = [];

    public static IReadOnlyList<InputPort> InputPorts => _inputPorts;
    public static IReadOnlyList<OutputPort> OutputPorts => _outputPorts;
    public static IReadOnlyList<Lamp> Lamps => _lamps;
    public static IReadOnlyList<Gate> Gates => _gates;
    public static IReadOnlyList<Wire> Wires => _wires;

    private static readonly Dictionary<int, IConnectable> _components = [];

    internal static IReadOnlyDictionary<int, IConnectable> Components => _components;

    internal static void AddEdge(IConnectable from, IConnectable to)
    {
        from.Fanout.Add(to);
        to.Fanin.Add(from);
    }

    internal static T AddNode<T>(T node) where T : IConnectable
    {
        _components[node.Id] = node;

        switch (node)
        {
            case InputPort ip: _inputPorts.Add(ip); break;
            case OutputPort op: _outputPorts.Add(op); break;
            case Lamp l: _lamps.Add(l); break;
            case Gate g: _gates.Add(g); break;
            case Wire w: _wires.Add(w); break;
        }
        
        return node;
    }

    public static void Clean()
    {
        _inputPorts.Clear();
        _outputPorts.Clear();
        _lamps.Clear();
        _gates.Clear();
        _wires.Clear();

        _components.Clear();

        Array.Clear(_hash);
    }
}
