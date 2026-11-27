using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void ActiveStoneBlocks(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (tile.TileType == TileID.ActiveStoneBlock)
        {
            if (/*Main.tile[i, j - 1] != null &&*/ (!Main.tile[i, j - 1].HasTile || !TileID.Sets.PreventsActuationUnder[Main.tile[i, j - 1].TileType]) && WorldGen.CanKillTile(i, j))
            {
                tile.TileType = TileID.InactiveStoneBlock;
                WorldGen.SquareTileFrame(i, j);
                NetMessage.SendTileSquare(-1, i, j);
            }
        }
        else if (tile.TileType == TileID.InactiveStoneBlock)
        {
            tile.TileType = TileID.ActiveStoneBlock;
            WorldGen.SquareTileFrame(i, j);
            NetMessage.SendTileSquare(-1, i, j);
        }
    }
}
