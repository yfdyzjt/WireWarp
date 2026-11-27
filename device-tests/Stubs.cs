// Value-type tile handle with shared cell storage, as in tML. No Terraria assembly required.
// This tests WireWarp's own handlers, not the native methods called by them.
namespace Terraria
{
    public sealed class Cell { public short X,Y; public ushort Type; public bool Active; }
    public struct Tile
    {
        private readonly Cell c;
        private readonly int x,y;
        public Tile(Cell cell,int i,int j) { c=cell; x=i; y=j; }
        public short TileFrameX => c.X;
        public short TileFrameY { get=>c.Y; set { c.Y=value; Main.Trace.Add($"Y:{x}:{y}:{value}"); } }
        public ushort TileType { get=>c.Type; set { c.Type=value; Main.Trace.Add($"T:{x}:{y}:{value}"); } }
        public bool HasTile => c.Active;
    }
    public sealed class TileMap
    {
        public readonly Cell[,] Cells;
        private readonly Tile[,] handles;
        public TileMap(int w,int h) { Cells=new Cell[w,h]; handles=new Tile[w,h]; for(int x=0;x<w;x++) for(int y=0;y<h;y++) { Cells[x,y]=new Cell(); handles[x,y]=new Tile(Cells[x,y],x,y); } }
        public ref Tile this[int x,int y] => ref handles[x,y];
    }
    public static class Main
    {
        public static int maxTilesX=12,maxTilesY=12,netMode;
        public static TileMap tile=new(12,12);
        public static List<string> Trace=[];
    }
    public static class NetMessage
    {
        public static void SendTileSquare(int who,int x,int y,int size) => Main.Trace.Add($"net:{who}:{x}:{y}:{size}");
        public static void SendTileSquare(int who,int x,int y,int w,int h) => Main.Trace.Add($"net:{who}:{x}:{y}:{w}:{h}");
        public static void SendTileSquare(int who,int x,int y) => Main.Trace.Add($"net:{who}:{x}:{y}");
    }
    public static class Wiring
    {
        public static void ToggleCampFire(int i,int j,Tile tile,bool? force,bool skip) => Main.Trace.Add($"native-campfire:{i}:{j}:{tile.TileFrameY}:{force}:{skip}");
        public static void ToggleLamp(int i,int j,Tile tile,bool? force,bool skip) => Main.Trace.Add($"native-lamp:{i}:{j}:{tile.TileFrameY}:{force}:{skip}");
        public static void ToggleHangingLantern(int i,int j,Tile tile,bool? force,bool skip) => Main.Trace.Add($"native-lantern:{i}:{j}:{tile.TileFrameY}:{force}:{skip}");
    }
    public static class WorldGen
    {
        public static void SwitchMonolith(int i,int j) => Main.Trace.Add($"native-monolith:{i}:{j}");
        public static void SquareTileFrame(int i,int j) => Main.Trace.Add($"frame:{i}:{j}");
    }
}
namespace Terraria.ID
{
    public static class TileID
    {
        public const ushort Campfire=215, Grate=546, GrateClosed=557;
        public static class Sets { public static readonly bool[] Campfires=new bool[65536]; }
    }
    public static class NetmodeID { public const int Server=2; }
}
namespace WireWarp.Frontend.Shared
{
    public static class Runtime
    {
        public static bool IsOpen=>true;
        public static bool IsRun=>true;
        public static Action<string>? DiagnosticSink {get;set;}
    }
}
