using System.Diagnostics;
using System.Globalization;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared;

// Optional, main-thread-only counters. No per-output stopwatch or logging.
internal static class FrontendDiagnostics
{
    internal static bool ProfileEnabled { get; set; }
    internal static bool DeviceProfileEnabled { get; set; }
    private static readonly long[] DeviceRuns = new long[257], DeviceApplied = new long[257], DeviceTicks = new long[257];
    internal static void RecordDevice(int type, long applied, long ticks)
    { DeviceRuns[type]++; DeviceApplied[type] += applied; DeviceTicks[type] += ticks; }
    internal sealed record DeviceStat(string Type, long Runs, long Applied, double InclusiveMs);
    internal sealed record StageStat(string Stage, double TotalMs, double MaxMs);
    internal sealed record ProfileSnapshot(long Updates, long Inputs, long Runs, long Received, long Applied, long Missing, StageStat[] Stages, DeviceStat[] Devices);
    internal static ProfileSnapshot Snapshot() => new(_frames, _inputs, _runs, _received, _applied, _missing,
        Names.Select((n,i) => new StageStat(n,Sums[i],Maxima[i])).ToArray(),
        Enumerable.Range(0,257).Where(i => DeviceRuns[i] != 0).Select(i => new DeviceStat(
            i == 256 ? "unmapped" : ((OutputID)i).ToString(), DeviceRuns[i], DeviceApplied[i],
            DeviceTicks[i] * 1000.0 / Stopwatch.Frequency)).ToArray());
    internal static void CancelBenchmark(string reason) => BenchmarkSession.Cancel(reason);
    internal static bool CaptureEnabled { get; set; }
    private static long _frames, _runs, _received, _applied, _missing, _inputs;
    private static readonly string[] Names = ["wait+decode", "other", "body-latency", "RLE-decode", "apply", "pack+capture", "send", "tick+misc"];
    private static readonly double[] Sums = new double[Names.Length];
    private static readonly double[] Maxima = new double[Names.Length];
    private const int Capacity = 8192;
    private readonly record struct Entry(string Kind, long Stamp, long Tick, long Id, long A, long B, long C, long D);
    private static readonly Entry[] Entries = new Entry[Capacity];
    private static long _written, _origin;

    internal static void ResetWorld()
    {
        ProfileEnabled = CaptureEnabled = DeviceProfileEnabled = false;
        ResetProfile();
        _written = 0;
        _origin = Stopwatch.GetTimestamp();
    }

    internal static void ResetProfile()
    {
        _frames = _runs = _received = _applied = _missing = _inputs = 0;
        Array.Clear(Sums);
        Array.Clear(Maxima);
        Array.Clear(DeviceRuns); Array.Clear(DeviceApplied); Array.Clear(DeviceTicks);
    }

    private static void Add(int i, double value)
    {
        Sums[i] += value;
        Maxima[i] = Math.Max(Maxima[i], value);
    }

    internal static void Record(int runs, long received, long applied, long missing, int inputs,
        double wait, double other, double bodyLatency, double decode, double apply,
        double pack, double send, double tick)
    {
        _frames++;
        _runs += runs; _received += received; _applied += applied; _missing += missing; _inputs += inputs;
        Add(0, wait); Add(1, other); Add(2, bodyLatency); Add(3, decode);
        Add(4, apply); Add(5, pack); Add(6, send); Add(7, tick);
    }

    internal static string ProfileStatus()
    {
        string line = $"WW37 {Runtime.FrontendStatus} profile={ProfileEnabled} frames={_frames} input={_inputs} runs={_runs} received={_received} applied={_applied} missing={_missing}";
        if (_frames == 0) return line;
        for (int i = 0; i < Names.Length; i++)
            line += $" {Names[i]}={Sums[i] / _frames:F3}/{Maxima[i]:F3}ms";
        line += $" applyTotal={Sums[4]:F3}ms";
        return line;
    }

    internal static void StartCapture()
    {
        _written = 0;
        _origin = Stopwatch.GetTimestamp();
        CaptureEnabled = true;
    }

    internal static void Capture(string kind, long tick, long id, long a, long b, long c, long d)
    {
        if (!CaptureEnabled) return;
        Entries[(int)(_written % Capacity)] = new Entry(kind, Stopwatch.GetTimestamp(), tick, id, a, b, c, d);
        _written++;
    }

    internal static string DumpCapture()
    {
        CaptureEnabled = false;
        string path = Path.Combine(Path.GetDirectoryName(Access.Instance.WorldPathName) ?? ".",
            $"WireWarp-capture-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.csv");
        using var writer = new StreamWriter(path);
        writer.WriteLine("seq,elapsed_ms,kind,tick,message_id,a,b,c,d");
        long start = Math.Max(0, _written - Capacity);
        for (long seq = start; seq < _written; seq++)
        {
            var entry = Entries[(int)(seq % Capacity)];
            double ms = Stopwatch.GetElapsedTime(_origin, entry.Stamp).TotalMilliseconds;
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{seq},{ms:F6},{entry.Kind},{entry.Tick},{entry.Id},{entry.A},{entry.B},{entry.C},{entry.D}"));
        }
        return path;
    }
}
