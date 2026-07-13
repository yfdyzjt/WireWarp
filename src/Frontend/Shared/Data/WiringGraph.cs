using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Data;

public class WiringGraph
{
    private readonly List<Wire> _wires = [];
    private readonly List<Gate> _gates = [];
    private readonly List<Lamp> _lamps = [];
    private readonly List<Input> _inputs = [];
    private readonly List<InputPort> _inputPorts = [];
    private readonly List<Output> _outputs = [];
    private readonly List<OutputPort> _outputPorts = [];

    public IReadOnlyList<Wire> Wires => _wires;
    public IReadOnlyList<Gate> Gates => _gates;
    public IReadOnlyList<Lamp> Lamps => _lamps;
    public IReadOnlyList<Input> Inputs => _inputs;
    public IReadOnlyList<InputPort> InputPorts => _inputPorts;
    public IReadOnlyList<Output> Outputs => _outputs;
    public IReadOnlyList<OutputPort> OutputPorts => _outputPorts;

    // edge

    public static void AddEdge(IConnectable from, IConnectable to)
    {
        from.Fanout.Add(to);
        to.Fanin.Add(from);
    }

    public static void RemoveEdge(IConnectable from, IConnectable to)
    {
        from.Fanout.Remove(to);
        to.Fanin.Remove(from);
    }

    // node

    public Wire AddWire(WireID type)
    {
        var node = new Wire { Type = type };
        _wires.Add(node);
        return node;
    }

    public Gate AddGate(GateID type, int x, int y)
    {
        var node = new Gate { Type = type, X = x, Y = y };
        _gates.Add(node);
        return node;
    }

    public Lamp AddLamp(LampID type, int x, int y)
    {
        var node = new Lamp { Type = type, X = x, Y = y };
        _lamps.Add(node);
        return node;
    }

    public Input AddInput(InputID type, int x, int y)
    {
        var node = new Input { Type = type, X = x, Y = y };
        _inputs.Add(node);
        return node;
    }

    public InputPort AddInputPort()
    {
        var node = new InputPort();
        _inputPorts.Add(node);
        return node;
    }

    public Output AddOutput(OutputID type, int x, int y)
    {
        var node = new Output { Type = type, X = x, Y = y };
        _outputs.Add(node);
        return node;
    }

    public OutputPort AddOutputPort()
    {
        var node = new OutputPort();
        _outputPorts.Add(node);
        return node;
    }

    public void RemoveNode(IConnectable node)
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
            case InputPort ip: _inputPorts.Remove(ip); break;
            case Output o: _outputs.Remove(o); break;
            case OutputPort op: _outputPorts.Remove(op); break;
        }
    }
}
