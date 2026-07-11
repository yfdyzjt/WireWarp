using System.Runtime.CompilerServices;
using WireWarp.Frontend.Shared.Interfaces;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.tModLoader.Accessors;

public class TileAccessor : ITileAccessor
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetWorldWidth() => Terraria.Main.maxTilesX;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetWorldHeight() => Terraria.Main.maxTilesY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tile GetTile(int x, int y)
    {
        var t = Terraria.Main.tile[x, y];
        return new Tile
        {
            TileType = t.TileType,
            TileFrameX = t.TileFrameX,
            TileFrameY = t.TileFrameY,
            HasTile = t.HasTile,
            HasActuator = t.HasActuator,
            IsActuated = t.IsActuated,
            RedWire = t.RedWire,
            BlueWire = t.BlueWire,
            GreenWire = t.GreenWire,
            YellowWire = t.YellowWire,
        };
    }
}
