using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Chimney(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num18 = tile.TileFrameX % 54 / 18;
        int num19 = tile.TileFrameY % 54 / 18;
        int num20 = i - num18;
        int num21 = j - num19;
        int num22 = 54;
        if (Main.tile[num20, num21].TileFrameY >= 108)
            num22 = -108;

        for (int num23 = num20; num23 < num20 + 3; num23++)
        {
            for (int num24 = num21; num24 < num21 + 3; num24++)
            {
                // SkipWire(num23, num24);
                Main.tile[num23, num24].TileFrameY = (short)(Main.tile[num23, num24].TileFrameY + num22);
            }
        }

        NetMessage.SendTileSquare(-1, num20 + 1, num21 + 1, 3);
    }
}
