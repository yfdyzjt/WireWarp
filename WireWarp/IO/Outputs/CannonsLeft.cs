using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void CannonsLeft(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num31 = tile.TileFrameX % 72 / 18;
        int num32 = tile.TileFrameY % 54 / 18;
        int num33 = i - num31;
        int num34 = j - num32;
        int num35 = tile.TileFrameY / 54;
        int num38 = 54;
        if (num35 >= 8)
            num38 = 0;

        bool flag = false;
        if (num38 != 0)
        {
            for (int num39 = num33; num39 < num33 + 4; num39++)
            {
                for (int num40 = num34; num40 < num34 + 3; num40++)
                {
                    // SkipWire(num39, num40);
                    Main.tile[num39, num40].TileFrameY = (short)(Main.tile[num39, num40].TileFrameY + num38);
                }
            }

            flag = true;
        }

        if (flag)
            NetMessage.SendTileSquare(-1, num33, num34, 4, 3);
    }
}
