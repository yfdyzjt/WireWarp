using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Assign
{
    public static void Execute()
    {
        Access.Instance.Status("Assigning wiring...");
        AssignIds();
    }

    public static void AssignIds()
    {
        var id = 0;
        
        foreach (var inputPort in WiringGraph.InputPorts)
        { WiringGraph.Components[id] = inputPort; inputPort.Id = id++; }
        foreach (var outputPort in WiringGraph.OutputPorts) 
        { WiringGraph.Components[id] = outputPort; outputPort.Id = id++; }
        foreach (var lamp in WiringGraph.Lamps) 
        { WiringGraph.Components[id] = lamp; lamp.Id = id++; }
        foreach (var gate in WiringGraph.Gates) 
        { WiringGraph.Components[id] = gate; gate.Id = id++; }
        foreach (var wire in WiringGraph.Wires) 
        { WiringGraph.Components[id] = wire; wire.Id = id++; }
        foreach (var input in WiringGraph.Inputs) 
        { WiringGraph.Components[id] = input; input.Id = id++; }
        foreach (var output in WiringGraph.Outputs) 
        { WiringGraph.Components[id] = output; output.Id = id++; }
    }
}
