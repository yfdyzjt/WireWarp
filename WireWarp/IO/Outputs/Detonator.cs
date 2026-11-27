using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Detonator(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num43 = tile.TileFrameX % 36 / 18;
        int num44 = tile.TileFrameY % 36 / 18;
        int num45 = i - num43;
        int num46 = j - num44;
        int num47 = 36;
        if (Main.tile[num45, num46].TileFrameX >= 36)
            num47 = -36;

        for (int num48 = num45; num48 < num45 + 2; num48++)
        {
            for (int num49 = num46; num49 < num46 + 2; num49++)
            {
                // SkipWire(num48, num49);
                Main.tile[num48, num49].TileFrameX = (short)(Main.tile[num48, num49].TileFrameX + num47);
            }
        }

        NetMessage.SendTileSquare(-1, num45, num46, 2, 2);
    }
}
