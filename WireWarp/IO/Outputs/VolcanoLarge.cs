using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void VolcanoLarge(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num132;
        for (num132 = tile.TileFrameY / 18; num132 >= 2; num132 -= 2)
        {
        }

        num132 = j - num132;
        int num133 = tile.TileFrameX / 18;
        if (num133 > 1)
            num133 -= 2;

        num133 = i - num133;
        // SkipWire(num133, num132);
        // SkipWire(num133, num132 + 1);
        // SkipWire(num133 + 1, num132);
        // SkipWire(num133 + 1, num132 + 1);
        short num134 = (short)((Main.tile[num133, num132].TileFrameX != 0) ? (-36) : 36);
        for (int num135 = 0; num135 < 2; num135++)
        {
            for (int num136 = 0; num136 < 2; num136++)
            {
                Main.tile[num133 + num135, num132 + num136].TileFrameX += num134;
            }
        }

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, num133, num132, 2, 2);

        int num137 = ((num134 > 0) ? 4 : 3);
        Animation.NewTemporaryAnimation(num137, TileID.VolcanoLarge, num133, num132);
        NetMessage.SendTemporaryAnimation(-1, num137, TileID.VolcanoLarge, num133, num132);
    }
}
