using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Reporter
{
    internal const string WiresFanin = "Wires.Fanin";
    internal const string WiresFanout = "Wires.Fanout";
    internal const string WiresInputPort = "Wires.InputPort";
    internal const string WiresOutputPort = "Wires.OutputPort";
    internal const string WiresNormalLamp = "Wires.NormalLamp";
    internal const string WiresFaultLamp = "Wires.FaultLamp";
    internal const string WiresNormalGate = "Wires.NormalGate";
    internal const string WiresFaultGate = "Wires.FaultGate";

    internal const string NormalGatesLampWires = "NormalGates.LampWires";
    internal const string NormalGatesGateWires = "NormalGates.GateWires";
    internal const string NormalGatesLamps = "NormalGates.Lamps";

    internal const string FaultGatesNormalLampWires = "FaultGates.NormalLampWires";
    internal const string FaultGatesFaultLampWires = "FaultGates.FaultLampWires";
    internal const string FaultGatesGateWires = "FaultGates.GateWires";
    internal const string FaultGatesNormalLamps = "FaultGates.NormalLamps";
    internal const string FaultGatesFaultLamps = "FaultGates.FaultLamps";

    internal const string InputPortsWires = "InputPorts.Wires";
    internal const string OutputPortsWires = "OutputPorts.Wires";

    public static void Execute()
    {
        Report.Components.Clear();
        Report.Histograms.Clear();

        Report.Components["Input"] = CountByType(WiringGraph.Inputs, n => ((InputID)n.Type).ToString());
        Report.Components["InputPort"] = CountByType(WiringGraph.InputPorts, n => ((InputID)n.Type).ToString());
        Report.Components["Output"] = CountByType(WiringGraph.Outputs, n => ((OutputID)n.Type).ToString());
        Report.Components["OutputPort"] = CountByType(WiringGraph.OutputPorts, n => ((OutputID)n.Type).ToString());
        Report.Components["Lamp"] = CountByType(WiringGraph.Lamps, n => ((LampID)n.Type).ToString());
        Report.Components["Gate"] = CountByType(WiringGraph.Gates, n => ((GateID)n.Type).ToString());
        Report.Components["Wire"] = CountByType(WiringGraph.Wires, n => ((WireID)n.Type).ToString());

        CollectWires();
        CollectGates();
        CollectPorts();
    }

    private static void CollectWires()
    {
        foreach (var wire in WiringGraph.Wires)
        {
            AddHistogram(WiresFanin, wire.Fanin.Count);
            AddHistogram(WiresFanout, wire.Fanout.Count);
            AddHistogram(WiresInputPort, AdjacentCount(wire, x => x is InputPort));
            AddHistogram(WiresOutputPort, AdjacentCount(wire, x => x is OutputPort));
            AddHistogram(WiresNormalLamp, AdjacentCount(wire, x => x is Lamp { Type: not LampID.Fault }));
            AddHistogram(WiresFaultLamp, AdjacentCount(wire, x => x is Lamp { Type: LampID.Fault }));
            AddHistogram(WiresNormalGate, AdjacentCount(wire, x => x is Gate { Type: not GateID.Fault }));
            AddHistogram(WiresFaultGate, AdjacentCount(wire, x => x is Gate { Type: GateID.Fault }));
        }
    }

    private static void CollectGates()
    {
        foreach (var gate in WiringGraph.Gates)
        {
            if (gate.Type == GateID.Fault) CollectFaultGate(gate);
            else CollectNormalGate(gate);
        }
    }

    private static void CollectNormalGate(Gate gate)
    {
        var lamps = gate.Fanin.OfType<Lamp>().Where(l => l.Type != LampID.Fault).ToList();

        AddHistogram(NormalGatesLampWires, lamps.Sum(LampWireCount));
        AddHistogram(NormalGatesGateWires, gate.Fanout.OfType<Wire>().Count());
        AddHistogram(NormalGatesLamps, lamps.Count);
    }

    private static void CollectFaultGate(Gate gate)
    {
        var normalLamps = gate.Fanin.OfType<Lamp>().Where(l => l.Type != LampID.Fault).ToList();
        var faultLamps = gate.Fanin.OfType<Lamp>().Where(l => l.Type == LampID.Fault).ToList();

        AddHistogram(FaultGatesNormalLampWires, normalLamps.Sum(LampWireCount));
        AddHistogram(FaultGatesFaultLampWires, faultLamps.Sum(LampWireCount));
        AddHistogram(FaultGatesGateWires, gate.Fanout.OfType<Wire>().Count());
        AddHistogram(FaultGatesNormalLamps, normalLamps.Count);
        AddHistogram(FaultGatesFaultLamps, faultLamps.Count);
    }

    private static void CollectPorts()
    {
        foreach (var port in WiringGraph.InputPorts)
            AddHistogram(InputPortsWires, port.Fanout.OfType<Wire>().Count());

        foreach (var port in WiringGraph.OutputPorts)
            AddHistogram(OutputPortsWires, port.Fanin.OfType<Wire>().Count());
    }

    private static int LampWireCount(Lamp lamp) => lamp.Fanin.OfType<Wire>().Count();

    private static int AdjacentCount(IConnectable node, Func<IConnectable, bool> match) =>
        node.Fanin.Count(match) + node.Fanout.Count(match);

    private static Dictionary<string, int> CountByType(IEnumerable<IConnectable> nodes,
        Func<IConnectable, string> name)
    {
        var result = new Dictionary<string, int>();
        foreach (var node in nodes)
        {
            var key = name(node);
            result[key] = result.GetValueOrDefault(key) + 1;
        }
        return result;
    }

    private static void AddHistogram(string name, int count)
    {
        if (!Report.Histograms.TryGetValue(name, out var hist))
            Report.Histograms[name] = hist = [];
        hist[count] = hist.GetValueOrDefault(count) + 1;
    }
}
