using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void SillyBalloonMachine(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num11 = tile.TileFrameX % 54 / 18;
        int num12 = tile.TileFrameY % 54 / 18;
        int num13 = i - num11;
        int num14 = j - num12;
        int num15 = 54;
        if (Main.tile[num13, num14].TileFrameX >= 54)
            num15 = -54;

        for (int num16 = num13; num16 < num13 + 3; num16++)
        {
            for (int num17 = num14; num17 < num14 + 3; num17++)
            {
                // SkipWire(num16, num17);
                Main.tile[num16, num17].TileFrameX = (short)(Main.tile[num16, num17].TileFrameX + num15);
            }
        }

        NetMessage.SendTileSquare(-1, num13 + 1, num14 + 1, 3);
    }
}
