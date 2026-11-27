using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Gemsparks(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.TileType;
        if (type >= TileID.AmethystGemsparkOff && type <= TileID.AmberGemspark)
        {
            if (!tile.HasActuator)
            {
                if (type >= TileID.AmethystGemspark)
                    tile.TileType = (ushort)(type - 7);
                else
                    tile.TileType = (ushort)(type + 7);

                WorldGen.SquareTileFrame(i, j);
                NetMessage.SendTileSquare(-1, i, j);
            }
        }
    }
}
