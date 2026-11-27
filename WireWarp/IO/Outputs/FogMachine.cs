using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void FogMachine(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num125;
        for (num125 = tile.TileFrameX / 18; num125 >= 2; num125 -= 2)
        {
        }

        int num126;
        for (num126 = tile.TileFrameY / 18; num126 >= 2; num126 -= 2)
        {
        }

        int num127 = i - num125;
        int num128 = j - num126;
        int num129 = 36;
        if (Main.tile[num127, num128].TileFrameX >= 36)
            num129 = -36;

        for (int num130 = num127; num130 < num127 + 2; num130++)
        {
            for (int num131 = num128; num131 < num128 + 2; num131++)
            {
                // SkipWire(num130, num131);
                Main.tile[num130, num131].TileFrameX = (short)(Main.tile[num130, num131].TileFrameX + num129);
            }
        }

        NetMessage.SendTileSquare(-1, num127, num128, 2, 2);
    }
}
