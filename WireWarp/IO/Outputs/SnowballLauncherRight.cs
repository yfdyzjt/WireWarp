using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void SnowballLauncherRight(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num51 = tile.TileFrameX % 54 / 18;
        int num52 = tile.TileFrameY % 54 / 18;
        int num53 = i - num51;
        int num54 = j - num52;
        int num55 = tile.TileFrameX / 54;
        int num57 = 54;
        if (num55 >= 1 && num57 > 0)
            num57 = 0;

        if (num55 == 0 && num57 < 0)
            num57 = 0;

        bool flag3 = false;
        if (num57 != 0)
        {
            for (int num58 = num53; num58 < num53 + 3; num58++)
            {
                for (int num59 = num54; num59 < num54 + 3; num59++)
                {
                    // SkipWire(num58, num59);
                    Main.tile[num58, num59].TileFrameX = (short)(Main.tile[num58, num59].TileFrameX + num57);
                }
            }

            flag3 = true;
        }

        if (flag3)
            NetMessage.SendTileSquare(-1, num53, num54, 3, 3);
    }
}
