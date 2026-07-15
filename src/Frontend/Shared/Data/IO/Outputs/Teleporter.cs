namespace WireWarp.Frontend.Shared.Data;

public static class Teleporter
{
    public static Dictionary<OutputPort, (Output source, Output target)> Data { get; } = [];

    public static void Clear() => Data.Clear();
}
