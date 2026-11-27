// Host substitutes only. The actual Runtime, IOGraph, Access and RuntimeOutput are compiled from the release sources.
using WireWarp.Frontend.Shared.Data;
namespace WireWarp.Frontend.Shared.Data
{
    public class Component { public List<Component> Fanout { get; } = []; public List<Component> Fanin { get; } = []; }
    public class Input : Component { public InputID Type; }
    public class InputPort : Component { public int PortId; }
    public class Output : Component { public OutputID Type; }
    public class OutputPort : Component { public int PortId; }
    public class Wire : Component { public List<(int,int)> Drains = []; }
    public class Lamp : Component { public int Id; public (int,int) Origin; }
    public class Gate : Component { public int Id; public (int,int) Origin; }
    public static class WiringGraph
    {
        public static Dictionary<(int,int),Input> InputPos = [];
        public static Dictionary<(int,int),Output> OutputPos = [];
        public static List<OutputPort> OutputPorts = [];
        public static List<Lamp> Lamps = [];
        public static List<Gate> Gates = [];
        public static Dictionary<int,Component> Components = [];
        public static Dictionary<(int,int),Lamp> LampPos = [];
        public static Dictionary<(int,int),Gate> GatePos = [];
        public static ReadOnlyMemory<byte> Hash => new byte[32];
        public static void Build() {} public static void Clean() {} public static void Resolve() {} public static void SetHash(byte[] h) {}
    }
    public static class IOExtra { public static void Build() {} public static void Resolve() {} public static void Clean() {} }
    public static class Report
    {
        public static List<int> SkippedFaultGates = [];
        public static List<(string,double)> Stages = [];
        public static List<string> Errors = [];
        public static bool Success;
        public static void SetWorldPath(string p) {} public static void SetHash(byte[] h) {} public static void AddStage(string s,double n) {}
    }
}
namespace WireWarp.Frontend.Shared.Conversion { public static class Hash { public static byte[] Execute() => new byte[32]; } }
namespace WireWarp.Frontend.Shared.File
{
    public static class HeaderFile { public static bool MatchHash(string p,byte[] h) => true; }
    public static class IOFile
    {
        public static string PathName => "fake.wwio";
        public static void Save() {}
        public static void Load()
        {
            IOGraph.Clean();
            IOGraph.SetOutput(0,(1,2),OutputID.Campfires);
            IOGraph.SetOutput(2,(2,2),OutputID.Torches);
            IOGraph.SetOutput(3,(3,2),OutputID.None);
            IOGraph.SetInput((1,1),7,InputID.Switches);
        }
    }
    public static class WiringFile { public static string PathName => "fake.wwir"; public static void Save() {} public static void Load() {} }
    public static class WWorldFile { public static string PathName => "fake.wwld"; public static void Save() {} public static void Load() {} }
    public static class ReportFile { public static string PathName => "fake.report"; public static void Write() {} }
}
namespace WireWarp.Frontend.Shared
{
    public static class Transport
    {
        public static bool IsOpen { get; private set; }
        public static string EncodingStatus => "test";
        public static string BackendMode => "test";
        public static long OutputPayloadBytes => 0;
        public static long CompactFrames => 0;
        public static long RleFrames => 0;
        public static double LatencyTime => 0;
        public static long LastCompletedFrameId => 1;
        public static long LastSentFrameId => 2;
        public static double LastFrameBodyLatency => 0;
        public static double LastFrameDecodeMs => 0;
        public static IReadOnlyList<(int portId,int count)> Next = [];
        public static void Open() => IsOpen = true;
        public static void Close() => IsOpen = false;
        public static (int,string) SendStartup() => (0,"");
        public static (int,string) SendShutdown() => (0,"");
        public static (int,string) SendReset() => (0,"");
        public static (int,string) SendSyncTo(byte[] h,string p) => (0,"");
        public static ((int status,string message) ack,(byte[] hash,string path) payload) SendSyncFrom() => ((0,""),(new byte[32],""));
        public static ((int status,string message) ack,IReadOnlyList<(int portId,int count)> payload) CompleteFrame() => ((0,""),Next);
        public static void SendFrameAsync(bool run,long tick,IReadOnlyList<(int portId,int count)> inputs) {}
    }
}
namespace WireWarp.Frontend.tModLoader.IO
{
    internal static partial class RuntimeOutput
    {
        internal static List<string> Calls = [];
        internal static int State;
        internal static Random Rng = new(7);
        internal static int FailAt = -1;
        internal static int StopAt = -1;
        internal static void Init() { Calls.Clear(); State=0; Rng=new(7); FailAt=StopAt=-1; }
        private static void Campfires(int id,int x,int y) => Act(id,x,y);
        private static void Torches(int id,int x,int y) => Act(id,x,y);
        private static void Act(int id,int x,int y)
        {
            if (Calls.Count == FailAt) throw new InvalidOperationException("intentional failure");
            State=unchecked(State*31+id+x+y+Rng.Next(100));
            Calls.Add($"{id}:{x}:{y}:{State}");
            if (Calls.Count == StopAt) WireWarp.Frontend.Shared.Runtime.Stop();
        }
    }
}
