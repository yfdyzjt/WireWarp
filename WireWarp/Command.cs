using Terraria.ModLoader;
using WireWarp.Frontend.tModLoader.IO;
using WireWarp.Frontend.Shared;

namespace WireWarp.Frontend.tModLoader;

internal sealed class WireWarpCommand : ModCommand
{
    public override CommandType Type => CommandType.World;
    public override string Command => "ww";
    public override string Usage => "/ww <startup|shutdown|run|stop|syncto|syncfrom|reset|report|status|trace|inspect|pulse|campfire|frontend|profile|capture|benchmark>";
    public override string Description => "WireWarp control commands";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 0)
        {
            caller.Reply(Usage);
            return;
        }

        try
        {
            switch (args[0])
            {
                case "status":
                    string status = Runtime.DiagnosticStatus() + " " + CampfireMode.Status + " " + Runtime.FrontendStatus;
                    caller.Reply(status);
                    Runtime.DiagnosticSink?.Invoke(status);
                    break;
                case "frontend":
                    if (args.Length == 1) { caller.Reply(Runtime.FrontendStatus); break; }
                    if (args.Length != 2 || (args[1] != "reference" && args[1] != "indexed" && args[1] != "compiled" && args[1] != "async" && args[1] != "compact" && args[1] != "fast"))
                    { caller.Reply("/ww frontend [reference|indexed|compiled|async|compact|fast]"); break; }
                    if (!Runtime.IsOpen) { caller.Reply("Load a WireWarp world first."); break; }
                    Runtime.SetFrontend(args[1]);
                    caller.Reply(Runtime.FrontendStatus + " (profile reset)");
                    break;
                case "benchmark":
                    if (args.Length == 1 || (args.Length == 2 && args[1] == "status"))
                    { caller.Reply(BenchmarkSession.LastResult); break; }
                    if (args.Length == 2 && args[1] == "cancel")
                    { BenchmarkSession.Cancel("user cancelled"); caller.Reply(BenchmarkSession.LastResult); break; }
                    if (args.Length < 3 || args.Length > 5 || args[1] != "start" || !int.TryParse(args[2], out int seconds) ||
                        (args.Length == 5 && args[4] != "devices"))
                    { caller.Reply("/ww benchmark start <5-300 seconds> [clock-label] [devices] | status | cancel"); break; }
                    BenchmarkSession.Start(seconds, args.Length >= 4 ? args[3] : "unspecified", CampfireMode.Status, args.Length == 5);
                    caller.Reply($"Benchmark started ({seconds}s). Leave the game UI visible; report exports automatically.");
                    break;
                case "profile":
                    if (args.Length == 1 || (args.Length == 2 && args[1] == "status"))
                    {
                        string report = FrontendDiagnostics.ProfileStatus();
                        caller.Reply(report);
                        Runtime.DiagnosticSink?.Invoke(report);
                        break;
                    }
                    if (BenchmarkSession.Active) { caller.Reply("Cancel benchmark before changing profiling."); break; }
                    if (args.Length != 2 || (args[1] != "on" && args[1] != "off" && args[1] != "reset"))
                    { caller.Reply("/ww profile [on|off|reset|status]"); break; }
                    if (!Runtime.IsOpen) { caller.Reply("Load a WireWarp world first."); break; }
                    if (args[1] == "on") { FrontendDiagnostics.ResetProfile(); FrontendDiagnostics.DeviceProfileEnabled = false; FrontendDiagnostics.ProfileEnabled = true; }
                    if (args[1] == "off") FrontendDiagnostics.ProfileEnabled = false;
                    if (args[1] == "reset") FrontendDiagnostics.ResetProfile();
                    caller.Reply(FrontendDiagnostics.ProfileStatus());
                    break;
                case "capture":
                    if (args.Length != 2 || (args[1] != "on" && args[1] != "off" && args[1] != "dump" && args[1] != "mark"))
                    { caller.Reply("/ww capture on|off|mark|dump"); break; }
                    if (!Runtime.IsOpen) { caller.Reply("Load a WireWarp world first."); break; }
                    if (args[1] == "on") { BenchmarkSession.Cancel("capture enabled"); FrontendDiagnostics.StartCapture(); caller.Reply("WireWarp capture active (8192 recent records)."); }
                    if (args[1] == "off") { FrontendDiagnostics.CaptureEnabled = false; caller.Reply("WireWarp capture stopped."); }
                    if (args[1] == "mark") { FrontendDiagnostics.Capture("mark", Runtime.Time, 0, 0, 0, 0, 0); caller.Reply(FrontendDiagnostics.CaptureEnabled ? "Marked current tick." : "Capture is off."); }
                    if (args[1] == "dump") caller.Reply("WireWarp capture saved: " + FrontendDiagnostics.DumpCapture());
                    break;
                case "campfire":
                    if (args.Length == 1)
                    {
                        caller.Reply(CampfireMode.Status);
                        break;
                    }
                    if (args.Length != 2 || (args[1] != "legacy" && args[1] != "native" && args[1] != "latest"))
                    {
                        caller.Reply("/ww campfire [native|legacy]");
                        break;
                    }
                    if (!Runtime.IsOpen)
                    {
                        caller.Reply("Load a WireWarp world before changing campfire mode.");
                        break;
                    }
                    BenchmarkSession.Cancel("campfire mode command");
                    if (!CampfireMode.TrySetLegacy(args[1] == "legacy"))
                    {
                        caller.Reply("Campfires already activated. Reload a clean world before changing mode.");
                        break;
                    }
                    caller.Reply(CampfireMode.Status);
                    Runtime.DiagnosticSink?.Invoke(CampfireMode.Status);
                    break;
                case "trace":
                    if (args.Length != 2 || (args[1] != "on" && args[1] != "off")) { caller.Reply("/ww trace on|off"); break; }
                    if (args[1] == "on") BenchmarkSession.Cancel("trace enabled");
                    Runtime.TraceEnabled = args[1] == "on";
                    caller.Reply($"WireWarp trace: {Runtime.TraceEnabled}");
                    break;
                case "inspect":
                case "pulse":
                    if (args.Length != 3 || !int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y)) { caller.Reply("/ww inspect|pulse x y"); break; }
                    caller.Reply(Runtime.DescribeInput(x,y));
                    if (args[0] == "pulse") caller.Reply(Runtime.HitInput(x,y) ? "Queued mapped input" : "Input not queued");
                    break;
                case "startup":
                    Runtime.Startup();
                    caller.Reply("WireWarp started");
                    break;

                case "shutdown":
                    Runtime.Shutdown();
                    caller.Reply("WireWarp shutdown");
                    break;

                case "run":
                    Runtime.Run();
                    caller.Reply("WireWarp running");
                    break;

                case "stop":
                    Runtime.Stop();
                    caller.Reply("WireWarp stopped");
                    break;

                case "syncto":
                    Runtime.SyncTo();
                    caller.Reply("WireWarp synced to backend");
                    break;

                case "syncfrom":
                    Runtime.SyncFrom();
                    caller.Reply("WireWarp synced from backend");
                    break;

                case "reset":
                    Runtime.Reset();
                    caller.Reply("WireWarp reset");
                    break;

                case "report":
                    Runtime.Report();
                    caller.Reply("WireWarp report written");
                    break;

                default:
                    caller.Reply(Usage);
                    break;
            }
        }
        catch (Exception e)
        {
            caller.Reply($"WireWarp command failed: {e.Message}");
            ModContent.GetInstance<WireWarp>().Logger.Error($"WireWarp command failed: {e}");
        }
    }
}
