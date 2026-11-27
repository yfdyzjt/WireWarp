#nullable enable annotations
using System.Reflection;
using WireWarp.Frontend.Shared;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.tModLoader.IO;

internal static partial class RuntimeOutput
{
    private static readonly Action<int, int, int>?[] _outputs =
        new Action<int, int, int>?[Enum.GetValues<OutputID>().Length];

    static RuntimeOutput()
    {
        foreach (var method in typeof(RuntimeOutput).GetMethods(
            BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (Enum.TryParse<OutputID>(method.Name, out var id))
                _outputs[(int)id] = method.CreateDelegate<Action<int, int, int>>();
        }
    }

    public static Action<int, int, int>? ResolveHandler(OutputID type) => _outputs[(int)type];

    public static void Execute(OutputID type, int portId, int i, int j)
        => _outputs[(int)type]?.Invoke(portId, i, j);

    public static void ExecuteRun(OutputID type, int portId, int i, int j, int count, ref long applied)
    {
        if (count <= 0 || !Runtime.IsOpen || !Runtime.IsRun) return;
        var handler = _outputs[(int)type];
        for (int k = 0; k < count && Runtime.IsOpen && Runtime.IsRun; k++)
        {
            // Same delegate as Execute, including null handler semantics and partial counts on exceptions.
            // No caching of tile frames, RNG, cooldowns, or furniture state.
            handler?.Invoke(portId, i, j);
            applied++;
        }
    }
}
