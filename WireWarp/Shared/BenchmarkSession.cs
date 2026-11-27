using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace WireWarp.Frontend.Shared;

// Main-thread callbacks only. Measures draw callbacks, not GPU presents.
internal static class BenchmarkSession
{
    private static long _start, _end;
    private static int _seconds;
    private static string _label = "", _mode = "", _campfire = "", _backend = "", _graph = "";
    private static bool _devices;
    private static int[] _gc = new int[3];
    private static long _allocated, _payloadBytes, _compactFrames, _rleFrames;
    private static readonly List<long> Draws = new(), Updates = new();
    internal static bool Active { get; private set; }
    internal static string LastResult { get; private set; } = "No benchmark exported.";

    internal static void Start(int seconds, string label, string campfire, bool devices)
    {
        if (seconds < 5 || seconds > 300) throw new ArgumentOutOfRangeException(nameof(seconds), "Use 5–300 seconds.");
        if (!Runtime.IsOpen || !Runtime.IsRun) throw new InvalidOperationException("Start a running WireWarp world first.");
        if (Active) throw new InvalidOperationException("Benchmark already running.");
        if (Runtime.TraceEnabled || FrontendDiagnostics.CaptureEnabled) throw new InvalidOperationException("Disable trace and capture for benchmarking.");
        Draws.Clear(); Updates.Clear();
        Draws.EnsureCapacity(seconds * 120); Updates.EnsureCapacity(seconds * 120);
        _seconds = seconds; _label = label; _mode = Runtime.FrontendMode; _campfire = campfire; _devices = devices;
        _backend = Transport.BackendMode;
        _graph = Convert.ToHexString(Runtime.Hash.Span).ToLowerInvariant();
        FrontendDiagnostics.ResetProfile();
        FrontendDiagnostics.DeviceProfileEnabled = devices;
        FrontendDiagnostics.ProfileEnabled = true;
        for (int i = 0; i < 3; i++) _gc[i] = GC.CollectionCount(i);
        _allocated = GC.GetTotalAllocatedBytes(false);
        _payloadBytes = Transport.OutputPayloadBytes; _compactFrames = Transport.CompactFrames; _rleFrames = Transport.RleFrames;
        _start = Stopwatch.GetTimestamp(); _end = 0;
        Active = true;
        LastResult = "Benchmark running.";
    }

    internal static void Draw()
    {
        if (Active) Draws.Add(Stopwatch.GetTimestamp());
    }

    internal static void Update()
    {
        if (!Active) return;
        if (!Runtime.IsOpen || !Runtime.IsRun || Runtime.FrontendMode != _mode || Transport.BackendMode != _backend || Runtime.TraceEnabled || FrontendDiagnostics.CaptureEnabled)
        { Cancel("execution settings changed"); return; }
        long now = Stopwatch.GetTimestamp();
        Updates.Add(now);
        if (Stopwatch.GetElapsedTime(_start, now).TotalSeconds >= _seconds) Finish(now);
    }

    internal static void Cancel(string why)
    {
        if (!Active) return;
        Active = false;
        FrontendDiagnostics.ProfileEnabled = false;
        FrontendDiagnostics.DeviceProfileEnabled = false;
        LastResult = "Benchmark cancelled: " + why;
        Runtime.DiagnosticSink?.Invoke(LastResult);
    }

    // Completed inter-callback intervals; excludes partial first/last intervals.
    internal static IntervalStats Summarize(IReadOnlyList<long> stamps)
    {
        if (stamps.Count < 2) return new(stamps.Count, 0, null, null, null, null, null, null, null, null);
        var intervals = new double[stamps.Count - 1];
        double total = 0;
        for (int i = 1; i < stamps.Count; i++)
        { intervals[i-1] = Stopwatch.GetElapsedTime(stamps[i-1], stamps[i]).TotalMilliseconds; total += intervals[i-1]; }
        Array.Sort(intervals);
        int n = intervals.Length;
        int tail = Math.Max(1, (int)Math.Ceiling(n * .01));
        double slow = 0;
        for (int i = n-tail; i < n; i++) slow += intervals[i];
        return new(stamps.Count, n, total > 0 ? 1000 * n / total : null,
            total / n, intervals[(int)Math.Ceiling(n * .95)-1], intervals[(int)Math.Ceiling(n * .99)-1],
            intervals[^1], slow > 0 ? 1000 * tail / slow : null,
            intervals[^1] > 0 ? 1000 / intervals[^1] : null, intervals[0] > 0 ? 1000 / intervals[0] : null);
    }
    internal sealed record IntervalStats(int Callbacks, int Intervals, double? RateHz, double? MeanMs, double? P95Ms,
        double? P99Ms, double? MaxMs, double? OnePercentLowHz, double? MinInstantHz, double? MaxInstantHz);

    private static void Finish(long end)
    {
        Active = false; _end = end;
        FrontendDiagnostics.ProfileEnabled = false;
        FrontendDiagnostics.DeviceProfileEnabled = false;
        double elapsed = Stopwatch.GetElapsedTime(_start, _end).TotalSeconds;
        var gc = Enumerable.Range(0,3).Select(i => GC.CollectionCount(i)-_gc[i]).ToArray();
        long allocated = GC.GetTotalAllocatedBytes(false)-_allocated;
        var draw = Summarize(Draws); var update = Summarize(Updates);
        var profile = FrontendDiagnostics.Snapshot();
        var report = new { schema = 2, version = "4.2", frontend = _mode, clock_label = _label,
            campfire_at_start = _campfire, graph_sha256 = _graph,
            backend_mode = _backend, requested_seconds = _seconds, elapsed_seconds = elapsed, device_profiling = _devices,
            draw_callback_kind = "PostDrawInterface (UI-visible proxy; not GPU presentation)",
            ipc = new { capability = Transport.EncodingStatus,
                output_payload_bytes = Transport.OutputPayloadBytes - _payloadBytes,
                compact_frames = Transport.CompactFrames - _compactFrames,
                rle_frames = Transport.RleFrames - _rleFrames },
            draw, update, profile, draw_callbacks_per_wall_second = Draws.Count / elapsed,
            update_callbacks_per_wall_second = Updates.Count / elapsed,
            input_activations_per_second = profile.Inputs / elapsed,
            output_activations_per_second = profile.Applied / elapsed,
            gc_collections = gc, process_managed_allocated_bytes = allocated };
        try
        {
            string dir = Path.Combine(Path.GetDirectoryName(Access.Instance.WorldPathName) ?? ".", "WireWarp-Benchmarks",
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{_mode}");
            Directory.CreateDirectory(dir);
            string json = Path.Combine(dir, "summary.json");
            global::System.IO.File.WriteAllText(json, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            using (var writer = new StreamWriter(Path.Combine(dir, "intervals.csv")))
            {
                writer.WriteLine("kind,elapsed_ms,interval_ms");
                WriteIntervals(writer, "draw", Draws); WriteIntervals(writer, "update", Updates);
            }
            LastResult = string.Create(CultureInfo.InvariantCulture,
                $"WW42 benchmark {_mode} {_label}: {elapsed:F2}s; draw FPS proxy={draw.RateHz:F2}, 1% low={draw.OnePercentLowHz:F2}; updates/s={update.RateHz:F2}; inputs/s={profile.Inputs/elapsed:F2}; applied/s={profile.Applied/elapsed:F0}; missing={profile.Missing}. Saved: {dir}");
            global::System.IO.File.WriteAllText(Path.Combine(dir, "summary.txt"), LastResult + Environment.NewLine + FrontendDiagnostics.ProfileStatus());
        }
        catch (Exception e) { LastResult = "Benchmark export failed: " + e.Message; }
        Runtime.DiagnosticSink?.Invoke(LastResult);
        Access.Instance.Notify(LastResult);
    }

    private static void WriteIntervals(StreamWriter writer, string kind, List<long> times)
    {
        for (int i = 0; i < times.Count; i++)
        {
            double t = Stopwatch.GetElapsedTime(_start, times[i]).TotalMilliseconds;
            string delta = i == 0 ? "" : Stopwatch.GetElapsedTime(times[i-1], times[i]).TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture);
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{kind},{t:F6},{delta}"));
        }
    }
}
