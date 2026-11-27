using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Grates(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (tile.TileType == TileID.Grate)
            tile.TileType = TileID.GrateClosed;
        else
            tile.TileType = TileID.Grate;

        WorldGen.SquareTileFrame(i, j);
        NetMessage.SendTileSquare(-1, i, j);
    }
}
