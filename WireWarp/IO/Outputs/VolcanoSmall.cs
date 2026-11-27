using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void VolcanoSmall(int portId, int i, int j)
    {
        // SkipWire(i, j);
        short num93 = (short)((Main.tile[i, j].TileFrameX != 0) ? (-18) : 18);
        Main.tile[i, j].TileFrameX += num93;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, i, j, 1, 1);

        int num94 = ((num93 > 0) ? 4 : 3);
        Animation.NewTemporaryAnimation(num94, TileID.VolcanoSmall, i, j);
        NetMessage.SendTemporaryAnimation(-1, num94, TileID.VolcanoSmall, i, j);
    }
}
