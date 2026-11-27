using WireWarp.Frontend.Shared;
using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.tModLoader.IO;
using Terraria;
using Terraria.ID;
using Game=Terraria.Main;

TileID.Sets.Campfires[TileID.Campfire]=true;
TileID.Sets.Campfires[999]=true; // another native campfire type must not use legacy
var rng=new Random(39);
var types=new[]{OutputID.Chimney,OutputID.Campfires,OutputID.Monoliths,OutputID.Lamps,OutputID.HangingLanterns,OutputID.Grates,OutputID.None};
string Run(int seed, bool fast, bool legacy, int mode, (OutputID type,int x,int y)[] events)
{
    var random=new Random(seed);
    Game.tile=new TileMap(12,12); Game.netMode=mode; Game.Trace.Clear();
    Runtime.DiagnosticSink=s=>Game.Trace.Add(s);
    CampfireMode.ResetWorld(); CampfireMode.TrySetLegacy(legacy);
    ushort[] tileTypes=[0,215,999,406,546,557,480];
    short[] frames=[-32768,-32760,-109,-108,-54,-36,-18,-1,0,17,18,35,36,53,54,107,108,161,162,32760,32767];
    for(int x=0;x<12;x++) for(int y=0;y<12;y++) {
        var c=Game.tile.Cells[x,y]; c.X=frames[random.Next(frames.Length)]; c.Y=frames[random.Next(frames.Length)];
        c.Type=tileTypes[random.Next(tileTypes.Length)]; c.Active=random.Next(4)!=0;
    }
    // Force one known active historical and one other campfire; include both at edge and interior.
    Game.tile.Cells[5,5].Type=215; Game.tile.Cells[5,5].Active=true;
    Game.tile.Cells[6,6].Type=999; Game.tile.Cells[6,6].Active=true;
    Game.tile.Cells[0,0].Type=215; Game.tile.Cells[0,0].Active=true;
    Game.tile.Cells[0,0].X=36; Game.tile.Cells[0,0].Y=18;
    foreach(var (type,x,y) in events) {
        try { (fast?FastDeviceOutput.Resolve(type):RuntimeOutput.ResolveHandler(type))?.Invoke(0,x,y); }
        catch(Exception e) { Game.Trace.Add("exception:"+e.GetType().Name); }
    }
    return string.Join(";",Game.Trace)+"|"+CampfireMode.Status+"|"+
        string.Join(";",Game.tile.Cells.Cast<Cell>().Select(c=>$"{c.X}:{c.Y}:{c.Type}:{c.Active}"));
}
foreach(var t in types.Where(t=>t!=OutputID.Chimney && t!=OutputID.Campfires))
    if(FastDeviceOutput.Resolve(t)!=RuntimeOutput.ResolveHandler(t)) throw new Exception("Native fallback changed: "+t);
for(int i=0;i<2000;i++) {
    var events=new List<(OutputID,int,int)> { (OutputID.Campfires,5,5),(OutputID.Campfires,6,6),(OutputID.Campfires,0,0), (OutputID.Chimney,5,5),(OutputID.Chimney,5,5) };
    for(int j=0;j<35;j++) events.Add((types[rng.Next(types.Length)],rng.Next(12),rng.Next(12)));
    var a=Run(i,false,i%2==0,i%3,events.ToArray()); var b=Run(i,true,i%2==0,i%3,events.ToArray());
    if(a!=b) throw new Exception("Mismatch at seed "+i+"\n"+a+"\n"+b);
}
Console.WriteLine("PASS: 2,000 mixed actual-handler sequences on a stub tile map (80,000 activations per path), write order, native delegation, network calls, bounds, diagnostics and final cells.");
