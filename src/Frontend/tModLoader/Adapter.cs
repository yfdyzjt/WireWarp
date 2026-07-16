using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.tModLoader;

internal sealed class Adapter : ITerraria
{
    public int MaxTilesX => Terraria.Main.maxTilesX;
    public int MaxTilesY => Terraria.Main.maxTilesY;

    public Tile Tile(int x, int y)
    {
        var real = Terraria.Main.tile[x, y];
        if (real == null)
            return default;

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
}
