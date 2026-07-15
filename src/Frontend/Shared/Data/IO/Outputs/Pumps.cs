namespace WireWarp.Frontend.Shared.Data;

public static class Pumps
{
    public static Dictionary<OutputPort, (List<Output> inlets, List<Output> outlets)> Data { get; } = [];

    public static void Clear() => Data.Clear();
}
