using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Validate
{
    public static bool Execute()
    {
        Access.Instance.Status("Validating wiring...");

        var start = Report.Errors.Count;

        ValidateConstraints();
        ValidateSymmetry();
        ValidateFaultGates();
        ValidateSkipWire();

        var count = Report.Errors.Count - start;
        if (count > 0) Report.AddError($"validation found {count} error(s)");

        return count == 0;
    }

    private static void ValidateConstraints()
    {
        var total = WiringGraph.Components.Count;
        var i = 0;
        foreach (var node in WiringGraph.Components.Values)
        {
            if (i++ % Math.Max(1, total / 100) == 0)
                Access.Instance.Status($"Validating constraints {i * 100 / total}%");

            switch (node)
            {
                case Input:
                    if ((InputID)node.Type == InputID.None)
                        Report.AddError($"{At(node)} Type expect input");
                    if (node.Fanin.Count != 0)
                        Report.AddError($"{At(node)} Fanin expect 0, got {node.Fanin.Count}");
                    if (node.Fanout.Count != 1)
                        Report.AddError($"{At(node)} Fanout expect 1, got {node.Fanout.Count}");
                    if (!node.Fanout.All(x => x is InputPort))
                        Report.AddError($"{At(node)} Fanout expect InputPort");
                    break;

                case InputPort:
                    if ((InputID)node.Type == InputID.None)
                        Report.AddError($"{At(node)} Type expect input port");
                    if (node.Fanin.Count != 1)
                        Report.AddError($"{At(node)} Fanin expect 1, got {node.Fanin.Count}");
                    if (!node.Fanin.All(x => x is Input))
                        Report.AddError($"{At(node)} Fanin expect Input");
                    if (node.Fanout.Count < 1)
                        Report.AddError($"{At(node)} Fanout expect >= 1, got {node.Fanout.Count}");
                    if (!node.Fanout.All(x => x is Wire))
                        Report.AddError($"{At(node)} Fanout expect Wire");
                    break;

                case Output:
                    if ((OutputID)node.Type == OutputID.None)
                        Report.AddError($"{At(node)} Type expect output");
                    if (node.Fanin.Count < 1)
                        Report.AddError($"{At(node)} Fanin expect >= 1, got {node.Fanin.Count}");
                    if (!node.Fanin.All(x => x is OutputPort))
                        Report.AddError($"{At(node)} Fanin expect OutputPort");
                    if (node.Fanout.Count != 0)
                        Report.AddError($"{At(node)} Fanout expect 0, got {node.Fanout.Count}");
                    break;

                case OutputPort:
                    if ((OutputID)node.Type == OutputID.None)
                        Report.AddError($"{At(node)} Type expect output port");
                    if (node.Fanin.Count < 1)
                        Report.AddError($"{At(node)} Fanin expect >= 1, got {node.Fanin.Count}");
                    if (!node.Fanin.All(x => x is Wire))
                        Report.AddError($"{At(node)} Fanin expect Wire");
                    if (node.Fanout.Count != 1)
                        Report.AddError($"{At(node)} Fanout expect 1, got {node.Fanout.Count}");
                    if (!node.Fanout.All(x => x is Output))
                        Report.AddError($"{At(node)} Fanout expect Output");
                    break;

                case Lamp:
                    if ((LampID)node.Type == LampID.None)
                        Report.AddError($"{At(node)} Type expect lamp");
                    if (!node.Fanin.All(x => x is Wire))
                        Report.AddError($"{At(node)} Fanin expect Wire");
                    if (node.Fanout.Count != 1)
                        Report.AddError($"{At(node)} Fanout expect 1, got {node.Fanout.Count}");
                    if (!node.Fanout.All(x => x is Gate))
                        Report.AddError($"{At(node)} Fanout expect Gate");
                    break;

                case Gate:
                    if ((GateID)node.Type == GateID.None)
                        Report.AddError($"{At(node)} Type expect gate");
                    if (node.Fanin.Count < 1)
                        Report.AddError($"{At(node)} Fanin expect >= 1, got {node.Fanin.Count}");
                    if (!node.Fanin.All(x => x is Lamp))
                        Report.AddError($"{At(node)} Fanin expect Lamp");
                    if (node.Fanout.Count < 1)
                        Report.AddError($"{At(node)} Fanout expect >= 1, got {node.Fanout.Count}");
                    if (!node.Fanout.All(x => x is Wire))
                        Report.AddError($"{At(node)} Fanout expect Wire");
                    break;

                case Wire:
                    if ((WireID)node.Type == WireID.None)
                        Report.AddError($"{At(node)} Type expect wire");
                    if (node.Fanin.Count < 1)
                        Report.AddError($"{At(node)} Fanin expect >= 1, got {node.Fanin.Count}");
                    if (!node.Fanin.All(x => x is Gate || x is InputPort))
                        Report.AddError($"{At(node)} Fanin expect Gate or InputPort");
                    if (node.Fanout.Count < 1)
                        Report.AddError($"{At(node)} Fanout expect >= 1, got {node.Fanout.Count}");
                    if (!node.Fanout.All(x => x is Lamp || x is OutputPort))
                        Report.AddError($"{At(node)} Fanout expect Lamp or OutputPort");
                    break;
            }
        }
    }

    private static void ValidateFaultGates()
    {
        foreach (var gate in WiringGraph.Gates.Where(g => g.Type == GateID.Fault))
        {
            var faultLamps = gate.Fanin.OfType<Lamp>()
                .Where(l => l.Type == LampID.Fault)
                .ToList();

            if (gate.Fanin.Count < 2)
                Report.AddError($"{At(gate)} lamp expect >= 2, got {gate.Fanin.Count}");
            if (faultLamps.Count != 1)
                Report.AddError($"{At(gate)} expect exactly 1 fault lamp, got {faultLamps.Count}");
        }
    }

    private static void ValidateSkipWire()
    {
        var total = WiringGraph.Wires.Count;
        var i = 0;
        foreach (var wire in WiringGraph.Wires)
        {
            if (i++ % Math.Max(1, total / 100) == 0)
                Access.Instance.Status($"Validating skip wire {i * 100 / total}%");

            var inputs = new HashSet<Input>();
            foreach (var pos in wire.Sources)
            {
                if (WiringGraph.GatePos.ContainsKey(pos)) continue;
                if (!WiringGraph.InputPos.TryGetValue(pos, out var input))
                    Report.AddError($"{At(wire)} source point ({pos.X},{pos.Y}) not found in InputPos");
                else if (!inputs.Add(input))
                    Report.AddError($"{At(wire)} input {At(input)} connected by multiple source points");
            }

            var outputs = new HashSet<Output>();
            foreach (var pos in wire.Drains)
            {
                if (WiringGraph.LampPos.ContainsKey(pos)) continue;
                if (!WiringGraph.OutputPos.TryGetValue(pos, out var output))
                    Report.AddError($"{At(wire)} drain point ({pos.X},{pos.Y}) not found in OutputPos");
                else if (!outputs.Add(output))
                    Report.AddError($"{At(wire)} output {At(output)} connected by multiple drain points");
            }
        }
    }

    private static void ValidateSymmetry()
    {
        var total = WiringGraph.Components.Count;
        var i = 0;
        foreach (var node in WiringGraph.Components.Values)
        {
            if (i++ % Math.Max(1, total / 100) == 0)
                Access.Instance.Status($"Validating symmetry {i * 100 / total}%");

            foreach (var target in node.Fanout)
                if (!target.Fanin.Contains(node))
                    Report.AddError($"{At(node)} edge asymmetry: {At(target)}");

            foreach (var source in node.Fanin)
                if (!source.Fanout.Contains(node))
                    Report.AddError($"{At(source)} edge asymmetry: {At(node)}");
        }
    }

    internal static string At(IConnectable node) => node switch
    {
        Input i => $"Input:{(InputID)node.Type}#{i.Id}@{i.Origin}",
        InputPort ip => $"InputPort:{(InputID)node.Type}#{ip.Id}",
        Output o => $"Output:{(OutputID)node.Type}#{o.Id}@{o.Origin}",
        OutputPort op => $"OutputPort:{(OutputID)node.Type}#{op.Id}",
        Lamp l => $"Lamp:{(LampID)node.Type}#{l.Id}@{l.Origin}",
        Gate g => $"Gate:{(GateID)node.Type}#{g.Id}@{g.Origin}",
        Wire w => $"Wire:{(WireID)node.Type}#{w.Id}",
        _ => $"#{node.Id}"
    };
}
