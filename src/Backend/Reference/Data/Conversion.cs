namespace WireWarp.Backend.Reference.Data;

public static class Conversion
{
    public static void Execute()
    {
        var inputPortCount = WiringGraph.InputPorts.Count;
        var outputPortCount = WiringGraph.OutputPorts.Count; 
        var lampCount = WiringGraph.Lamps.Count;
        var gateCount = WiringGraph.Gates.Count;
        var wireCount = WiringGraph.Wires.Count;

        Netlist.SetCounts(inputPortCount, outputPortCount, lampCount, gateCount, wireCount);
    }
}