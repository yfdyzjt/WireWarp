namespace WireWarp.Backend.Reference.Data;

public static class Netlist
{
    private static readonly byte[] _hash = new byte[32];

    public static ReadOnlySpan<byte> Hash => _hash;

    internal static void SetHash(ReadOnlySpan<byte> hash) => hash.CopyTo(_hash);    

    private static int[] _sourceWireOffsets = [];
    private static int[] _sourceWireSegments = [];
    private static int[] _wireFaultOffsets = [];
    private static int[] _wireFaultSegments = [];
    private static int[] _wireNormOffsets = [];
    private static int[] _wireNormSegments = [];
    private static int[] _wireOutputOffsets = [];
    private static int[] _wireOutputSegments = [];

    public static ReadOnlySpan<int> SourceWireOffsets => _sourceWireOffsets;
    public static ReadOnlySpan<int> SourceWireSegments => _sourceWireSegments;
    public static ReadOnlySpan<int> WireFaultOffsets => _wireFaultOffsets;
    public static ReadOnlySpan<int> WireFaultSegments => _wireFaultSegments;
    public static ReadOnlySpan<int> WireNormOffsets => _wireNormOffsets;
    public static ReadOnlySpan<int> WireNormSegments => _wireNormSegments;
    public static ReadOnlySpan<int> WireOutputOffsets => _wireOutputOffsets;
    public static ReadOnlySpan<int> WireOutputSegments => _wireOutputSegments;

    internal static void SetWireTables(
        int[] sourceWireOffsets, int[] sourceWireSegments,
        int[] wireFaultOffsets, int[] wireFaultSegments,
        int[] wireNormOffsets, int[] wireNormSegments,
        int[] wireOutputOffsets, int[] wireOutputSegments)
    {
        _sourceWireOffsets = sourceWireOffsets; _sourceWireSegments = sourceWireSegments;
        _wireFaultOffsets = wireFaultOffsets; _wireFaultSegments = wireFaultSegments;
        _wireNormOffsets = wireNormOffsets; _wireNormSegments = wireNormSegments;
        _wireOutputOffsets = wireOutputOffsets; _wireOutputSegments = wireOutputSegments;
    }

    private static GateType[] _gateTypes = [];
    private static int[] _gateWireLampBaseOffsets = [];
    private static byte[] _gateWireLampBaseSegments = [];
    private static int[] _gateWireRefOffsets = [];
    private static int[] _gateWireRefSegments = [];

    public static ReadOnlySpan<GateType> GateTypes => _gateTypes;
    public static ReadOnlySpan<int> GateWireLampBaseOffsets => _gateWireLampBaseOffsets;
    public static ReadOnlySpan<byte> GateWireLampBaseSegments => _gateWireLampBaseSegments;
    public static ReadOnlySpan<int> GateWireRefOffsets => _gateWireRefOffsets;
    public static ReadOnlySpan<int> GateWireRefSegments => _gateWireRefSegments;

    public static void SetGateTables(
        GateType[] gateTypes,
        int[] gateWireLampBaseOffsets, byte[] gateWireLampBaseSegments,
        int[] gateWireRefOffsets, int[] gateWireRefSegments)
    {
        _gateTypes = gateTypes;
        _gateWireLampBaseOffsets = gateWireLampBaseOffsets; _gateWireLampBaseSegments = gateWireLampBaseSegments;
        _gateWireRefOffsets = gateWireRefOffsets; _gateWireRefSegments = gateWireRefSegments;
    }

    private static int _inputPortCount;
    private static int _outputPortCount;
    private static int _lampCount;
    private static int _gateCount;
    private static int _wireCount;

    public static int InputPortCount => _inputPortCount;
    public static int OutputPortCount => _outputPortCount;
    public static int LampCount => _lampCount;
    public static int GateCount => _gateCount;
    public static int WireCount => _wireCount;

    public static void SetCounts(int inputPortCount, int outputPortCount, 
        int lampCount, int gateCount, int wireCount)
    {
        _inputPortCount = inputPortCount; _outputPortCount = outputPortCount;
        _lampCount = lampCount; _gateCount = gateCount; _wireCount = wireCount;
    }

    public static void Build()
    {
        Clean();
        
        Conversion.Execute();
    }

    public static void Clean()
    {
        Array.Clear(_hash);

        _sourceWireOffsets = [];
        _sourceWireSegments = [];
        _wireFaultOffsets = [];
        _wireFaultSegments = [];
        _wireNormOffsets = [];
        _wireNormSegments = [];
        _wireOutputOffsets = [];
        _wireOutputSegments = [];

        _gateTypes = [];
        _gateWireLampBaseOffsets = [];
        _gateWireLampBaseSegments = [];
        _gateWireRefOffsets = [];
        _gateWireRefSegments = [];

        _inputPortCount = 0;
        _outputPortCount = 0;
        _lampCount = 0;
        _gateCount = 0;
        _wireCount = 0;
    }
}

public enum GateType : byte
{
    Norm_1 = 0,
    Norm_2_AND, // and, nand, or, nor
    Norm_2_XOR, // xor, xnor
    Norm_n_AND,
    Norm_n_XOR,
    Fault_1,
    Fault_n,
}