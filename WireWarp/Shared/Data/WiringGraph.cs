using System.Diagnostics;
using WireWarp.Frontend.Shared.Conversion;

namespace WireWarp.Frontend.Shared.Data;

public static class WiringGraph
{
    private static readonly byte[] _hash = new byte[32];

    public static ReadOnlyMemory<byte> Hash => _hash;

    internal static void SetHash(byte[] hash) => hash.CopyTo(_hash, 0);

    private static readonly HashSet<Wire> _wires = [];
    private static readonly HashSet<Gate> _gates = [];
    private static readonly HashSet<Lamp> _lamps = [];
    private static readonly HashSet<Input> _inputs = [];
    private static readonly HashSet<InputPort> _inputPorts = [];
    private static readonly HashSet<Output> _outputs = [];
    private static readonly HashSet<OutputPort> _outputPorts = [];

    public static int InputPortOffset => 0;
    public static int OutputPortOffset => InputPortOffset + _inputPorts.Count;
    public static int LampOffset => OutputPortOffset + _outputPorts.Count;
    public static int GateOffset => LampOffset + _lamps.Count;
    public static int WireOffset => GateOffset + _gates.Count;
    public static int InputOffset => WireOffset + _wires.Count;
    public static int OutputOffset => InputOffset + _inputs.Count;

    public static IReadOnlySet<Wire> Wires => _wires;
    public static IReadOnlySet<Gate> Gates => _gates;
    public static IReadOnlySet<Lamp> Lamps => _lamps;
    public static IReadOnlySet<Input> Inputs => _inputs;
    public static IReadOnlySet<InputPort> InputPorts => _inputPorts;
    public static IReadOnlySet<Output> Outputs => _outputs;
    public static IReadOnlySet<OutputPort> OutputPorts => _outputPorts;

    public static Dictionary<int, IConnectable> Components { get; } = [];

    internal static Dictionary<(int x, int y), Gate> GatePos { get; } = [];
    internal static Dictionary<(int x, int y), Lamp> LampPos { get; } = [];
    internal static Dictionary<(int x, int y), Input> InputPos { get; } = [];
    internal static Dictionary<(int x, int y), Output> OutputPos { get; } = [];

    internal static void AddEdge(IConnectable from, IConnectable to)
    {
        from.Fanout.Add(to);
        to.Fanin.Add(from);
    }

    internal static void RemoveEdge(IConnectable from, IConnectable to)
    {
        from.Fanout.Remove(to);
        to.Fanin.Remove(from);
    }
    
    internal static IConnectable CopyNode(IConnectable node)
    {
        IConnectable copy = node switch
        {
            Wire w => new Wire { Id = w.Id, Type = w.Type },
            Gate g => new Gate { Id = g.Id, Type = g.Type, Origin = g.Origin },
            Lamp l => new Lamp { Id = l.Id, Type = l.Type, Origin = l.Origin },
            Input i => new Input { Id = i.Id, Type = i.Type, Origin = i.Origin },
            Output o => new Output { Id = o.Id, Type = o.Type, Origin = o.Origin },
            InputPort ip => new InputPort { Id = ip.Id },
            OutputPort op => new OutputPort { Id = op.Id },
            _ => throw new InvalidOperationException($"Unknown node type {node.GetType().Name}")
        };

        if (node is Wire sourceWire && copy is Wire copyWire)
        {
            foreach (var (x, y) in sourceWire.Sources) copyWire.Sources.Add((x, y));
            foreach (var (x, y) in sourceWire.Drains) copyWire.Drains.Add((x, y));
        }

        AddNode(copy);

        foreach (var source in node.Fanin)
            AddEdge(source, copy);

        foreach (var target in node.Fanout)
            AddEdge(copy, target);

        return copy;
    }

    internal static T AddNode<T>(T node) where T : IConnectable
    {
        switch (node)
        {
            case Wire w: _wires.Add(w); break;
            case Gate g: _gates.Add(g); break;
            case Lamp l: _lamps.Add(l); break;
            case Input i: _inputs.Add(i); break;
            case Output o: _outputs.Add(o); break;
            case InputPort ip: _inputPorts.Add(ip); break;
            case OutputPort op: _outputPorts.Add(op); break;
        }
        return node;
    }

    internal static void RemoveNode(IConnectable node)
    {
        foreach (var source in node.Fanin)
            source.Fanout.Remove(node);

        foreach (var target in node.Fanout)
            target.Fanin.Remove(node);

        node.Fanin.Clear();
        node.Fanout.Clear();

        switch (node)
        {
            case Wire w: _wires.Remove(w); break;
            case Gate g: _gates.Remove(g); break;
            case Lamp l: _lamps.Remove(l); break;
            case Input i: _inputs.Remove(i); break;
            case Output o: _outputs.Remove(o); break;
            case InputPort ip: _inputPorts.Remove(ip); break;
            case OutputPort op: _outputPorts.Remove(op); break;
        }
    }

    public static void Build()
    {
        Clean();
        Report.Clean();

        var sw = Stopwatch.StartNew();

        try
        {
            // preprocess
            Scan.Execute();
            Report.AddStage("BuildWiring.Scan", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Conversion.Trace.Execute();
            Report.AddStage("BuildWiring.Trace", sw.Elapsed.TotalMilliseconds); sw.Restart();

            // postprocess
            Prune.Execute();
            Report.AddStage("BuildWiring.Prune.1", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Normalize.Execute();
            Report.AddStage("BuildWiring.Normalize", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Prune.Execute();
            Report.AddStage("BuildWiring.Prune.2", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Applier.Execute();
            Report.AddStage("BuildWiring.Applier", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Prune.Execute();
            Report.AddStage("BuildWiring.Prune.3", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Assign.Execute();
            Report.AddStage("BuildWiring.Assign", sw.Elapsed.TotalMilliseconds); sw.Restart();

            if (!Validate.Execute()) throw new Exception("Wiring validation failed, see report.");
            Report.AddStage("BuildWiring.Validate", sw.Elapsed.TotalMilliseconds); sw.Restart();

            Reporter.Execute();
            Report.AddStage("BuildWiring.Reporter", sw.Elapsed.TotalMilliseconds); sw.Restart();
        }
        catch
        {
            Reporter.Execute();
            throw;
        }
    }

    public static void Resolve()
    {
        Apply.Execute();
    }

    public static void Clean()
    {
        _wires.Clear();
        _gates.Clear();
        _lamps.Clear();
        _inputs.Clear();
        _inputPorts.Clear();
        _outputs.Clear();
        _outputPorts.Clear();

        Components.Clear();
        GatePos.Clear();
        LampPos.Clear();
        InputPos.Clear();
        OutputPos.Clear();

        Array.Clear(_hash);

        WiringExtra.Clean();
        WiringTemp.Clean();
    }
}
