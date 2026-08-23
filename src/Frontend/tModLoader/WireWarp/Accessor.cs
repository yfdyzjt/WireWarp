using WireWarp.Frontend.Shared;
using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria;
using WireWarp.Frontend.tModLoader.IO;

namespace WireWarp.Frontend.tModLoader;

internal sealed class Accessor : Access
{
    // File

    public override string WorldPathName => Terraria.Main.worldPathName;

    public override void SaveWorld(bool resetTime = false, bool useTemps = false, bool canBeSkipped = false) => 
        Terraria.IO.WorldFile.SaveWorld(resetTime, useTemps, canBeSkipped);
    public override void LoadWorld() => 
        Terraria.IO.WorldFile.LoadWorld();

    // Preprocess

    public override int MaxTilesX => Terraria.Main.maxTilesX;
    public override int MaxTilesY => Terraria.Main.maxTilesY;

    public override Tile GetTile(int x, int y)
    {
        var real = Terraria.Main.tile[x, y];
        return new Tile
        {
            type = real.TileType,
            frameX = real.TileFrameX,
            frameY = real.TileFrameY,
            HasTile = real.HasTile,
            HasActuator = real.HasActuator,
            IsActuated = real.IsActuated,
            RedWire = real.RedWire,
            BlueWire = real.BlueWire,
            GreenWire = real.GreenWire,
            YellowWire = real.YellowWire,
        };
    }

    public override void SetTile(int x, int y, Tile tile)
    {
        var real = Terraria.Main.tile[x, y];
        real.TileType = tile.type;
        real.TileFrameX = tile.frameX;
        real.TileFrameY = tile.frameY;
        real.HasTile = tile.HasTile;
        real.HasActuator = tile.HasActuator;
        real.IsActuated = tile.IsActuated;
        real.RedWire = tile.RedWire;
        real.BlueWire = tile.BlueWire;
        real.GreenWire = tile.GreenWire;
        real.YellowWire = tile.YellowWire;
    }

    // Runtime

    public override void Execute(InputID type, int portId, int i, int j) => 
        RuntimeInput.Execute(type, portId, i, j);
    public override void Execute(OutputID type, int portId, int i, int j) => 
        RuntimeOutput.Execute(type, portId, i, j);

    public override void Tick() => RuntimeGeneral.Tick();
    public override void Reset() => RuntimeGeneral.Reset();

    public override void Status(string message) => Terraria.Main.statusText = message;
    public override void Notify(string message) => Terraria.Main.NewText(message);
}
