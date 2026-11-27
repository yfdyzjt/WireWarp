using WireWarp.Frontend.Shared;
using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria;
using WireWarp.Frontend.tModLoader.IO;

var accessor = new TestAccess();
Access.Instance = accessor;
// World startup must reset all optional instrumentation and select the fast frontend.
for (int i = 0; i < 2; i++)
{
    Runtime.SetFrontend("reference");
    Runtime.TraceEnabled = true;
    FrontendDiagnostics.ProfileEnabled = FrontendDiagnostics.CaptureEnabled = FrontendDiagnostics.DeviceProfileEnabled = true;
    Runtime.Startup();
    if (Runtime.FrontendMode != "fast" || !Runtime.DeviceFrontend || !Runtime.CompactFrontend || !Runtime.AsyncFrontend || Runtime.TraceEnabled || FrontendDiagnostics.ProfileEnabled || FrontendDiagnostics.CaptureEnabled || FrontendDiagnostics.DeviceProfileEnabled)
        throw new Exception("World startup defaults incorrect");
    Runtime.Shutdown();
}
Console.WriteLine("PASS: fast frontend and disabled diagnostics on startup and reopen.");
string Run(string mode, IReadOnlyList<(int,int)> runs, int fail=-1, int stop=-1, bool sparse=false)
{
    if (Runtime.IsOpen) Runtime.Shutdown();
    Runtime.Startup();
    Runtime.SetFrontend(mode);
    if (sparse) { IOGraph.SetOutput(-3,(5,6),OutputID.Torches); IOGraph.PrepareOutputIndex(); }
    accessor.Missing.Clear(); RuntimeOutput.Init(); RuntimeOutput.FailAt=fail; RuntimeOutput.StopAt=stop;
    Transport.Next = runs;
    string exception="";
    try { Runtime.Tick(); } catch (Exception e) { exception=e.GetType().Name+":"+e.Message; }
    return string.Join(";",RuntimeOutput.Calls)+"|"+string.Join(";",accessor.Missing)+"|"+Runtime.DiagnosticStatus()+"|"+exception+"|"+RuntimeOutput.Rng.Next();
}
void Compare(IReadOnlyList<(int,int)> runs,int fail=-1,int stop=-1,bool sparse=false)
{
    var a=Run("reference",runs,fail,stop,sparse);
    foreach (var mode in new[]{"indexed","compiled","async","compact","fast"})
    {
        var b=Run(mode,runs,fail,stop,sparse);
        if(a!=b) throw new Exception("Ordered parity failed: "+mode+"\n"+a+"\n"+b);
    }
}
Compare([(0,3),(2,4),(1,2),(3,7),(0,2),(5,0),(0,-1)]);
Compare([(0,4),(2,4)],fail:5);
Compare([(0,4),(2,4)],stop:2);
Compare([(-3,2),(0,4),(1,1)],sparse:true);
// Compact view vs ordinary run list, including missing mapping, stop, and failure.
var compactBytes = new byte[] { 250,255,255,255, 0,0, 2,0, 1,0, 2,0, 0,0 };
var compact = CompactOutputRuns.Parse(compactBytes);
var expanded = new List<(int,int)> { (0,1),(2,1),(1,1),(2,1),(0,1) };
if (!compact.SequenceEqual(expanded)) throw new Exception("Compact order");
foreach(var mode in new[]{"reference","indexed","compiled","async","compact","fast"})
{
    if(Run(mode,compact)!=Run("reference",expanded)) throw new Exception("Compact runtime mismatch");
    if(Run(mode,compact,stop:2)!=Run("reference",expanded,stop:2)) throw new Exception("Compact stop mismatch");
    if(Run(mode,compact,fail:2)!=Run("reference",expanded,fail:2)) throw new Exception("Compact failure mismatch");
}
foreach(var invalid in new[]{new byte[]{0,0,0,0},new byte[]{254,255,255,255},new byte[]{255,255,255,255,1}})
{
    try { CompactOutputRuns.Parse(invalid); throw new Exception("Accepted malformed compact payload"); }
    catch(InvalidDataException) {}
}
var rng=new Random(123);
var reusable=new List<(int portId,int count)>();
for(int i=0;i<1000;i++)
{
    var runs=new List<(int,int)>();
    for(int j=0;j<rng.Next(1,40);j++) runs.Add((rng.Next(-2,7),rng.Next(0,12)));
    Compare(runs);
}
Runtime.Shutdown();
for(int i=0;i<1000;i++)
{
    var expected=new List<(int,int)>();
    using var ms=new MemoryStream(); using var w=new BinaryWriter(ms);
    int n=rng.Next(0,100); w.Write(n);
    for(int j=0;j<n;j++) { int id=rng.Next(-10,100), repeat=rng.Next(-3,100); expected.Add((id,repeat)); w.Write(id); w.Write(repeat); }
    w.Write((byte)0x55); // trailing bytes are ignored by the original decoder
    var bytes=ms.ToArray();
    if(!FrameRunDecoder.TryDecode(bytes,out var actual)||!expected.SequenceEqual(actual)) throw new Exception("Decode parity failed");
    if(!FrameRunDecoder.TryDecodeInto(bytes,reusable)||!expected.SequenceEqual(reusable)) throw new Exception("Reusable decode parity failed");
    if(n>0 && FrameRunDecoder.TryDecode(bytes.AsSpan(0,bytes.Length-2),out _)) throw new Exception("Truncated decode accepted");
}
if(FrameRunDecoder.TryDecode(new byte[]{255,255,255,255},out _)) throw new Exception("Negative record count accepted");
Console.WriteLine("PASS: 1000 valid binary decoding checks + invalid-length fallback.");
var stats=BenchmarkSession.Summarize(new long[]{0,System.Diagnostics.Stopwatch.Frequency,System.Diagnostics.Stopwatch.Frequency*2});
if(stats.RateHz!=1 || stats.OnePercentLowHz!=1 || stats.P99Ms!=1000 || BenchmarkSession.Summarize(new long[]{0}).RateHz!=null) throw new Exception("Interval statistics incorrect");
Console.WriteLine("PASS: Reference/indexed/compiled/async/compact/fast comparisons; actual shared runtime and delegate dispatch; host stubs only.");
Console.WriteLine("PASS: interval summary and reusable decoder checks.");

sealed class TestAccess : Access
{
    internal List<string> Missing = [];
    public override string WorldPathName => "fake.wld";
    public override void SaveWorld(bool resetTime=false,bool useTemps=false,bool canBeSkipped=false) {}
    public override void LoadWorld() {}
    public override int MaxTilesX => 10;
    public override int MaxTilesY => 10;
    public override Tile GetTile(int x,int y) => default;
    public override void SetTile(int x,int y,Tile tile) {}
    public override void Execute(InputID type,int id,int x,int y) {}
    public override void Execute(OutputID type,int id,int x,int y) => RuntimeOutput.Execute(type,id,x,y);
    public override Action<int,int,int>? ResolveOutputHandler(OutputID type) => RuntimeOutput.ResolveHandler(type);
    public override void ExecuteOutputRun(OutputID type,int id,int x,int y,int count,ref long applied) => RuntimeOutput.ExecuteRun(type,id,x,y,count,ref applied);
    public override void Tick() {}
    public override void Reset() {}
    public override void Status(string message) {}
    public override void Notify(string message) { if(message.Contains("not found in Outputs")) Missing.Add(message); }
}
