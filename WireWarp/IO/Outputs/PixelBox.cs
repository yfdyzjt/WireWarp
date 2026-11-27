using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void PixelBox(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        tile.TileFrameX = (short)((tile.TileFrameX != 18) ? 18 : 0);
        NetMessage.SendTileSquare(-1, i, j);
    }
}
