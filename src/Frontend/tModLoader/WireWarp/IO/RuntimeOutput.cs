using System.Reflection;
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

    public static void Execute(OutputID type, int portId, int i, int j)
        => _outputs[(int)type]?.Invoke(portId, i, j);
}
