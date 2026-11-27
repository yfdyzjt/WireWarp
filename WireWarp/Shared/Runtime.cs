using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.File;

namespace WireWarp.Frontend.Shared;

public static class Runtime
{
    private const int FrameTimeoutWindow = 600;
    private const double FrameTimeoutBudget = 16.67;

    private static readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private static readonly FrameStats _frameStats = new();
    
    private static readonly byte[] _hash = new byte[32];
    public static ReadOnlyMemory<byte> Hash => _hash;

    private static bool _isOpen;
    private static bool _isRun;
    private static long _time;

    public static bool IsOpen => _isOpen;
    public static bool IsRun => _isRun;
    public static long Time => _time;

    public static Action<string>? DiagnosticSink { get; set; }
    public static bool TraceEnabled { get; set; }
    private static long _diagHits, _diagMapped, _diagSent, _diagReceived, _diagApplied, _diagMissing;
    private static long _diagFrames;
    public static string FrontendMode { get; private set; } = "fast";
    public static bool FastFrontend => FrontendMode != "reference";
    public static bool CompiledFrontend => FrontendMode == "compiled" || FrontendMode == "async" || CompactFrontend;
    public static bool CompactFrontend => FrontendMode == "compact" || DeviceFrontend;
    public static bool DeviceFrontend => FrontendMode == "fast";
    public static bool AsyncFrontend => FrontendMode == "async" || CompactFrontend;
    public static string FrontendStatus => $"frontend={FrontendMode} outputs={IOGraph.OutputIndexStatus} compiled={IOGraph.CompiledIndexStatus} ipc={Transport.EncodingStatus} backend={Transport.BackendMode} deviceFast={DeviceFrontend}";
    public static void SetFastFrontend(bool fast) => SetFrontend(fast ? "fast" : "reference");
    public static void SetFrontend(string mode)
    {
        if (mode != "reference" && mode != "indexed" && mode != "compiled" && mode != "async" && mode != "compact" && mode != "fast") throw new ArgumentException("Unknown frontend mode");
        FrontendDiagnostics.CancelBenchmark("frontend changed");
        FrontendMode = mode;
        FrontendDiagnostics.ResetProfile();
    }
    private static void Diag(string message) { if (TraceEnabled) DiagnosticSink?.Invoke(message); }
    public static string DiagnosticStatus() => $"open={_isOpen} run={_isRun} tick={_time} frames={_diagFrames} hits={_diagHits} mapped={_diagMapped} sent={_diagSent} received={_diagReceived} applied={_diagApplied} missing={_diagMissing}";
    public static string DescribeInput(int x, int y) => IOGraph.Inputs.TryGetValue((x,y), out var input)
        ? $"({x},{y}) -> port {input.portId}, {input.type}" : $"({x},{y}) not mapped";
    public static void ResetDiagnostics() { _diagHits = _diagMapped = _diagSent = _diagReceived = _diagApplied = _diagMissing = _diagFrames = 0; }


    public static void Run()
    {
        if (!_isOpen) throw new Exception("Frontend not open.");
        else _isRun = true;
    }

    public static void Stop()
    {
        FrontendDiagnostics.CancelBenchmark("Stop");
        if (!_isOpen) throw new Exception("Frontend not open.");
        else _isRun = false;
    }

    public static void Startup()
    {
        if (_isOpen) return;

        var sw = Stopwatch.StartNew();
        try
        {
            Access.Instance.Status("Updating files...");
            UpdateFile();

            Access.Instance.Status("Waiting for backend open...");
            Transport.Open();

            Access.Instance.Status("Waiting for backend sync...");
            CheckAck("Backend sync to failed", Transport.SendSyncTo(_hash, WiringFile.PathName));

            Access.Instance.Status("Waiting for backend startup...");
            CheckAck("Backend startup failed", Transport.SendStartup());

            _isOpen = true;
            _isRun = true;

            _time = 0;
            ResetDiagnostics();
            FrontendMode = "fast"; // validated RC2 fast path; reference remains selectable
            TraceEnabled = false;
            FrontendDiagnostics.ResetWorld();

            _frameStats.Reset();
            _frameTimer.Restart();

            Access.Instance.Reset();
            IOFrame.Clean();
        }
        catch
        { try { Shutdown(); } catch { } throw; }
        
        Access.Instance.Notify($"Frontend started in {sw.Elapsed.TotalSeconds:F2}s");
    }

    public static void Shutdown()
    {
        _isOpen = false;
        _isRun = false;
        FrontendDiagnostics.CancelBenchmark("world shutdown");

        _time = 0;

        _frameStats.Reset();
        
        Array.Clear(_hash);

        Access.Instance.Reset();
        IOFrame.Clean();

        IOGraph.Clean();
        WiringGraph.Clean();

        try
        {
            // Graph validation can fail before Transport.Open is reached.
            if (Transport.IsOpen)
                CheckAck("Backend shutdown failed", Transport.SendShutdown());
        }
        finally { Transport.Close(); }

        Access.Instance.Notify("Frontend shutdown");
    }

    public static void Tick()
    {
        if (!_isOpen) return;

        var other = _frameTimer.Elapsed.TotalMilliseconds;
        _frameTimer.Restart();

        try
        {
            var (ack, outputs) = Transport.CompleteFrame();
            var backend = Math.Max(0, Transport.LatencyTime);
            CheckAck("Backend frame failed", ack);

            var wait = _frameTimer.Elapsed.TotalMilliseconds;
            _frameTimer.Restart();

            if (_isRun)
            {
                bool measure = FrontendDiagnostics.ProfileEnabled;
                long start = measure ? Stopwatch.GetTimestamp() : 0;
                long received = 0;
                int outputRunCount = outputs.Count;
                if (outputs is CompactOutputRuns) received = outputRunCount;
                else if (CompiledFrontend && outputs is List<(int portId, int count)> list)
                    foreach (var run in CollectionsMarshal.AsSpan(list)) received += run.count;
                else if (CompiledFrontend)
                    for (int i = 0; i < outputs.Count; i++) received += outputs[i].count;
                else received = outputs.Sum(p => (long)p.count);
                _diagReceived += received;
                var before = _diagApplied;
                long missingBefore = _diagMissing;
                FrontendDiagnostics.Capture("apply-begin", _time, Transport.LastCompletedFrameId, outputs.Count, received, 0, 0);
                if (FrontendDiagnostics.DeviceProfileEnabled && measure) ApplyProfiledRuns(outputs);
                else if (DeviceFrontend) ApplyDeviceRuns(outputs);
                else if (CompiledFrontend) ApplyCompiledRuns(outputs);
                else if (FastFrontend) ApplyOutputRuns(outputs);
                else foreach (var output in UnPackRLE(outputs)) HitOutput(output);
                long appliedAt = measure ? Stopwatch.GetTimestamp() : 0;
                FrontendDiagnostics.Capture("apply", _time, Transport.LastCompletedFrameId,
                    outputs.Count, received, _diagApplied - before, _diagMissing - missingBefore);
                // The next async read may overwrite a borrowed compact response.
                // Snapshot every field needed later before starting that read.
                string outputSample = TraceEnabled ? string.Join(",", outputs.Take(8).Select(p => $"{p.portId}x{p.count}")) : "";
                var inputIds = IOFrame.ReadInputs();
                var packed = PackRLE(inputIds);
                int sentInputCount = inputIds.Count;
                long packedAt = measure ? Stopwatch.GetTimestamp() : 0;
                Transport.SendFrameAsync(true, _time, packed);
                long sentAt = measure ? Stopwatch.GetTimestamp() : 0;
                FrontendDiagnostics.Capture("send", _time, Transport.LastSentFrameId,
                    packed.Count, inputIds.Count, 0, 0);
                if (FrontendDiagnostics.CaptureEnabled)
                    foreach (var (portId, count) in packed)
                        FrontendDiagnostics.Capture("send-run", _time, Transport.LastSentFrameId, portId, count, 0, 0);
                _diagFrames++;
                _diagSent += inputIds.Count;
                if (TraceEnabled && (inputIds.Count != 0 || received != 0))
                    Diag($"frame {_time}: sent={inputIds.Count} [{string.Join(",", packed.Take(8).Select(p => $"{p.portId}x{p.count}"))}] received={received} applied={_diagApplied-before} output sample=[{outputSample}]");

                Access.Instance.Tick();
                IOFrame.Tick();
                if (measure)
                    FrontendDiagnostics.Record(outputRunCount, received, _diagApplied - before,
                        _diagMissing - missingBefore, sentInputCount, wait, other,
                        Transport.LastFrameBodyLatency, Transport.LastFrameDecodeMs,
                        Stopwatch.GetElapsedTime(start, appliedAt).TotalMilliseconds,
                        Stopwatch.GetElapsedTime(appliedAt, packedAt).TotalMilliseconds,
                        Stopwatch.GetElapsedTime(packedAt, sentAt).TotalMilliseconds,
                        Stopwatch.GetElapsedTime(sentAt).TotalMilliseconds);

                _time++;
            }
            else
            {
                Transport.SendFrameAsync(false, _time, []);
            }

            var frontend = _frameTimer.Elapsed.TotalMilliseconds;
            _frameStats.Record(frontend, backend, wait, other);

            if (_isRun && _time % FrameTimeoutWindow == 0)
            {
                if (_frameStats.HasTimeouts)
                    Access.Instance.Notify(_frameStats.Report());
                else
                    _frameStats.Reset();
            }
        }
        catch
        { try { Shutdown(); } catch { } throw; }

        _frameTimer.Restart();
    }

    public static bool HitInput(int x, int y, bool hitPoint = true)
    {
        _diagHits++;
        if (!_isOpen || !_isRun) { Diag($"input ({x},{y}) ignored: open={_isOpen} run={_isRun}"); return false; }

        if (IOGraph.Inputs.TryGetValue((x, y), out var input))
        {
            if (hitPoint) Access.Instance.Execute(input.type, input.portId, x, y);
            IOFrame.WriteInput(input.portId);
            _diagMapped++;
            FrontendDiagnostics.Capture("input", _time, 0, input.portId, 1, x, y);
            Diag($"input ({x},{y}) queued port={input.portId} type={input.type} hitPoint={hitPoint}");
            return true;
        }

        _diagMissing++;
        Diag($"input ({x},{y}) missing from IO map");
        Access.Instance.Notify($"Point ({x},{y}) not found in Inputs");
        return false;
    }

    private static void ApplyCompiledRuns(IReadOnlyList<(int portId, int count)> outputs)
    {
        // Compact IDs are borrowed from the completed receive buffer; no decoded list needed.
        if (outputs is CompactOutputRuns compact && BitConverter.IsLittleEndian)
        {
            foreach (ushort port in compact.NativeIds) ApplyCompiledRun(port, 1);
            return;
        }
        // No mutation of the decoded list during application. Transport only reuses it on the next CompleteFrame.
        if (outputs is List<(int portId, int count)> list)
        {
            foreach (var (port, count) in CollectionsMarshal.AsSpan(list)) ApplyCompiledRun(port, count);
            return;
        }
        for (int i = 0; i < outputs.Count; i++)
        {
            var (port, count) = outputs[i];
            ApplyCompiledRun(port, count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyCompiledRun(int port, int count)
    {
        if (count <= 0 || !_isOpen || !_isRun) return;
        if (IOGraph.TryGetCompiled(port, out var action))
        {
            var handler = action.Handler;
            if (count == 1)
            {
                handler?.Invoke(port, action.X, action.Y);
                _diagApplied++;
            }
            else for (int k = 0; k < count && _isOpen && _isRun; k++)
            {
                handler?.Invoke(port, action.X, action.Y);
                _diagApplied++;
            }
        }
        else ApplyIndexedRun(port, count); // sparse or missing mapping, original semantics
    }

    private static void ApplyDeviceRuns(IReadOnlyList<(int portId, int count)> outputs)
    {
        if (outputs is CompactOutputRuns compact && BitConverter.IsLittleEndian)
        {
            foreach (ushort port in compact.NativeIds) ApplyDeviceRun(port, 1);
            return;
        }
        if (outputs is List<(int portId, int count)> list)
        {
            foreach (var (port, count) in CollectionsMarshal.AsSpan(list)) ApplyDeviceRun(port, count);
            return;
        }
        for (int i = 0; i < outputs.Count; i++)
        {
            var (port, count) = outputs[i];
            ApplyDeviceRun(port, count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyDeviceRun(int port, int count)
    {
        if (count <= 0 || !_isOpen || !_isRun) return;
        var index = IOGraph.DeviceIndex;
        if (index == null || (uint)port >= (uint)index.Length || !index[port].Present)
        {
            ApplyIndexedRun(port, count); // sparse/missing: same fallback as v3.8
            return;
        }
        ref readonly var action = ref index[port];
        var handler = action.Handler;
        int x = action.X, y = action.Y;
        if (count == 1)
        {
            handler?.Invoke(port, x, y);
            _diagApplied++;
        }
        else for (int k = 0; k < count && _isOpen && _isRun; k++)
        {
            handler?.Invoke(port, x, y);
            _diagApplied++;
        }
    }

    // Opt-in attribution only: per-run clocks distort timing, never used in normal benchmark.
    private static void ApplyProfiledRuns(IReadOnlyList<(int portId, int count)> outputs)
    {
        foreach (var (port, count) in outputs)
        {
            if (count <= 0 || !_isOpen || !_isRun) continue;
            int type = IOGraph.TryGetOutput(port, out var mapping) ? (int)mapping.type : 256;
            long before = _diagApplied, start = Stopwatch.GetTimestamp();
            if (DeviceFrontend) ApplyDeviceRun(port, count);
            else if (CompiledFrontend) ApplyCompiledRun(port, count);
            else if (FastFrontend) ApplyIndexedRun(port, count);
            else for (int k = 0; k < count; k++) HitOutput(port);
            FrontendDiagnostics.RecordDevice(type, _diagApplied - before, Stopwatch.GetTimestamp() - start);
        }
    }

    private static void ApplyOutputRuns(IReadOnlyList<(int portId, int count)> outputs)
    {
        // Never merge even adjacent runs, skip pairs, or reorder side effects.
        // Missing ports take the original path, including per-activation notifications.
        foreach (var (portId, count) in outputs) ApplyIndexedRun(portId, count);
    }

    private static void ApplyIndexedRun(int portId, int count)
    {
            if (count <= 0 || !_isOpen || !_isRun) return;
            if (IOGraph.TryGetOutput(portId, out var output))
            {
                // RGB traces are predominantly single-hit runs; avoid the extra run loop.
                if (count == 1)
                {
                    Access.Instance.Execute(output.type, portId, output.pos.x, output.pos.y);
                    _diagApplied++;
                }
                else Access.Instance.ExecuteOutputRun(output.type, portId, output.pos.x, output.pos.y,
                    count, ref _diagApplied);
            }
            else
                for (int k = 0; k < count; k++) HitOutput(portId);
    }

    private static bool HitOutput(int portId)
    {
        if (!_isOpen || !_isRun) return false;

        if (IOGraph.Outputs.TryGetValue(portId, out var output))
        {
            Access.Instance.Execute(output.type, portId, output.pos.x, output.pos.y);
            _diagApplied++;
            return true;
        }

        _diagMissing++;
        Access.Instance.Notify($"Port ({portId}) not found in Outputs");
        return false;
    }

    public static void SyncTo()
    {
        FrontendDiagnostics.CancelBenchmark("SyncTo");
        if (!_isOpen) throw new Exception("Frontend not open.");

        var sw = Stopwatch.StartNew();

        try
        {
            Access.Instance.Notify("Saving world...");
            try { Access.Instance.SaveWorld(); }
            catch (Exception e) { throw new Exception($"Failed to save world: {e.Message}", e); }

            Access.Instance.Notify("Updating wiring files...");
            UpdateFile();

            Access.Instance.Notify("Waiting for backend...");
            CheckAck("Backend sync to failed", Transport.SendSyncTo(_hash, WiringFile.PathName));

            Access.Instance.Status("Backend initializing...");
            CheckAck("Backend startup failed", Transport.SendStartup());
        }
        catch
        {
            try { Shutdown(); } catch { }
            throw;
        }

        Access.Instance.Notify($"Wiring synced to backend in {sw.Elapsed.TotalSeconds:F2}s");
    }

    public static void SyncFrom()
    {
        FrontendDiagnostics.CancelBenchmark("SyncFrom");
        if (!_isOpen) throw new Exception("Frontend not open.");

        var sw = Stopwatch.StartNew();
        try
        {
            Access.Instance.Notify("Waiting for backend...");
            var (ack, payload) = Transport.SendSyncFrom();
            CheckAck("Backend sync from failed", ack);

            if (!payload.hash.AsSpan().SequenceEqual(_hash))
                throw new Exception("Wiring hash mismatch, sync failed.");

            Access.Instance.Notify("Applying wiring state to world...");
            WiringFile.Load();

            IOGraph.Resolve();
            WiringGraph.Resolve();
        }
        catch
        {
            try { Shutdown(); } catch { }
            throw;
        }

        Access.Instance.Notify($"Wiring state applied to world in {sw.Elapsed.TotalSeconds:F2}s");
    }

    public static void Reset()
    {
        FrontendDiagnostics.CancelBenchmark("Reset");
        if (!_isOpen) throw new Exception("Frontend not open.");

        var sw = Stopwatch.StartNew();
        try
        {
            CheckAck("Backend reset failed", Transport.SendReset());

            _time = 0;
            _frameStats.Reset();
            _frameTimer.Restart();

            Access.Instance.Reset();
            IOFrame.Clean();

            if (!HeaderFile.MatchHash(WWorldFile.PathName, _hash))
                throw new Exception("World snapshot hash mismatch, reset aborted.");

            Access.Instance.Notify("Loading world snapshot...");
            WWorldFile.Load();

            Access.Instance.Notify("Reloading world...");
            try { Access.Instance.LoadWorld(); }
            catch (Exception e) { throw new Exception($"Failed to reload world: {e.Message}", e);  }
        }
        catch
        {
            try { Shutdown(); } catch { }
            throw;
        }

        Access.Instance.Notify($"Reset complete in {sw.Elapsed.TotalSeconds:F2}s");
    }

    public static void Report()
    {
        ReportFile.Write();
        Access.Instance.Notify($"Report written to {ReportFile.PathName}");
    }

    private static void UpdateFile()
    {
        var sw = Stopwatch.StartNew();
        byte[] hash = [];

        try
        {
            Access.Instance.Status("Hashing wiring...");
            hash = Conversion.Hash.Execute();
            var hashTime = sw.Elapsed.TotalMilliseconds;
            sw.Restart();

            Data.Report.SetWorldPath(Access.Instance.WorldPathName);

            var built = false;
            if (!HeaderFile.MatchHash(WiringFile.PathName, hash) ||
                !HeaderFile.MatchHash(IOFile.PathName, hash))
            {
                built = true;

                Access.Instance.Status("Building wiring graph...");
                WiringGraph.Build();
                if (Data.Report.SkippedFaultGates.Count > 0)
                    DiagnosticSink?.Invoke($"Skipped {Data.Report.SkippedFaultGates.Count} malformed faulty gates; world tiles unchanged. See /ww report for coordinates.");
                
                Data.Report.Stages.Insert(0, ("Hash", hashTime));

                Data.Report.SetHash(hash);
                WiringGraph.SetHash(hash);

                Access.Instance.Status("Building io graph...");
                IOGraph.Build();
                Data.Report.AddStage("BuildIO", sw.Elapsed.TotalMilliseconds); sw.Restart();

                Access.Instance.Status("Saving wiring graph...");
                WiringFile.Save();
                Data.Report.AddStage("SaveWiring", sw.Elapsed.TotalMilliseconds); sw.Restart();

                Access.Instance.Status("Saving io graph...");
                IOFile.Save();
                Data.Report.AddStage("SaveIO", sw.Elapsed.TotalMilliseconds); sw.Restart();

                WiringGraph.Clean();
            }
            else
            {
                Access.Instance.Status("Loading io graph...");
                IOFile.Load();
            }

            IOGraph.PrepareOutputIndex();
            hash.AsSpan().CopyTo(_hash.AsSpan());

            Access.Instance.Status("Saving world snapshot...");
            WWorldFile.Save();

            if (built)
            {
                Data.Report.AddStage("Snapshot", sw.Elapsed.TotalMilliseconds);
                Data.Report.Success = Data.Report.Errors.Count == 0;
            }
        }
        catch (Exception e)
        {
            Data.Report.Success = false;
            if (hash.Length > 0) Data.Report.SetHash(hash);
            Data.Report.Errors.Add($"{e.GetType().Name}: {e.Message}");

            try { Report(); }
            catch (Exception reportException)
            { Access.Instance.Notify($"Report write failed: {reportException.Message}"); }

            throw;
        }
    }

    private static List<(int portId, int count)> PackRLE(IReadOnlyList<int> ids)
    {
        var result = new List<(int portId, int count)>();
        foreach (var id in ids)
        {
            if (result.Count > 0 && result[^1].portId == id)
                result[^1] = (id, result[^1].count + 1);
            else
                result.Add((id, 1));
        }
        return result;
    }

    private static IEnumerable<int> UnPackRLE(IReadOnlyList<(int portId, int count)> ids)
    {
        foreach (var (portId, count) in ids)
            for (var k = 0; k < count; k++)
                yield return portId;
    }

    private static void CheckAck(string prefix, (int status, string message) ack)
    {
        if (ack.status == 0) return;
        throw new Exception($"{prefix}: {ack.status} {ack.message}");
    }

    private sealed class FrameStats
    {
        private int _count;
        private int _timeouts;

        private double _fSum, _fMax;
        private double _bSum, _bMax;
        private double _wSum, _wMax;
        private double _oSum, _oMax;
        private double _tSum, _tMax;

        public bool HasTimeouts => _timeouts > 0;

        public void Record(double frontend, double backend, double wait, double other)
        {
            var total = frontend + wait + other;

            _count++;
            if (total > FrameTimeoutBudget) _timeouts++;

            _fSum += frontend; _fMax = Math.Max(_fMax, frontend);
            _bSum += backend; _bMax = Math.Max(_bMax, backend);
            _wSum += wait; _wMax = Math.Max(_wMax, wait);
            _oSum += other; _oMax = Math.Max(_oMax, other);
            _tSum += total; _tMax = Math.Max(_tMax, total);
        }

        public string Report()
        {
            var line = $"Slow frames: " +
                       $"F:{_fSum / _count,5:F2}/{_fMax,5:F2}ms, " +
                       $"B:{_bSum / _count,5:F2}/{_bMax,5:F2}ms, " +
                       $"W:{_wSum / _count,5:F2}/{_wMax,5:F2}ms, " +
                       $"O:{_oSum / _count,5:F2}/{_oMax,5:F2}ms, " +
                       $"T:{_tSum / _count,5:F2}/{_tMax,5:F2}ms";
            Reset();
            return line;
        }

        public void Reset()
        {
            _count = 0;
            _timeouts = 0;
            _fSum = _fMax = _bSum = _bMax = _wSum = _wMax = _oSum = _oMax = _tSum = _tMax = 0;
        }
    }
}
