namespace WireWarp.Frontend.Shared.Data;

public static class Report
{
    private static string _worldPath = "";
    private static byte[] _hash = [];

    public static string WorldPath => _worldPath;
    public static byte[] Hash => _hash;

    public static bool Success { get; internal set; } = true;

    public static List<(string name, double time)> Stages { get; } = [];
    public static List<string> Errors { get; } = [];
    public static List<(int x, int y)> SkippedFaultGates { get; } = [];
    public static Dictionary<string, int> Pruned { get; } = [];

    public static Dictionary<string, Dictionary<string, int>> Components { get; } = [];
    public static Dictionary<string, Dictionary<int, int>> Histograms { get; } = [];

    public static void Clean()
    {
        _hash = [];

        Stages.Clear();
        Errors.Clear();
        SkippedFaultGates.Clear();
        Components.Clear();
        Pruned.Clear();
        Histograms.Clear();

        Success = true;
    }

    public static void SetWorldPath(string worldPath) => _worldPath = worldPath;

    public static void SetHash(byte[] hash) => _hash = hash;

    public static void AddStage(string name, double time) => Stages.Add((name, time));

    public static void AddError(string message) => Errors.Add(message);

    public static void AddSkippedFaultGate(int x, int y) => SkippedFaultGates.Add((x, y));

    public static void AddPruned(string component) => Pruned[component] = Pruned.GetValueOrDefault(component) + 1;
}
