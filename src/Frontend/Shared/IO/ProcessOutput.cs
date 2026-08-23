using System.Reflection;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

internal static partial class ProcessOutput
{
    private static readonly Action<Output>?[] _processors =
        new Action<Output>?[Enum.GetValues<OutputID>().Length];

    static ProcessOutput()
    {
        foreach (var method in typeof(ProcessOutput).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (Enum.TryParse<OutputID>(method.Name, out var id))
                _processors[(int)id] = method.CreateDelegate<Action<Output>>();
        }
    }

    public static void Execute()
    {
        var total = WiringGraph.Outputs.Count;
        var i = 0;
        foreach (var output in WiringGraph.Outputs)
        {
            if (i++ % Math.Max(1, total / 100) == 0)
                Access.Instance.Status($"Applying outputs {i * 100 / total}%");
            _processors[(int)output.Type]?.Invoke(output);
        }
    }
}
