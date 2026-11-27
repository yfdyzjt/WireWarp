using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void BubbleMachine(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num81;
        for (num81 = tile.TileFrameX / 18; num81 >= 3; num81 -= 3)
        {
        }

        int num82;
        for (num82 = tile.TileFrameY / 18; num82 >= 3; num82 -= 3)
        {
        }

        int num83 = i - num81;
        int num84 = j - num82;
        int num85 = 54;
        if (Main.tile[num83, num84].TileFrameX >= 54)
            num85 = -54;

        for (int num86 = num83; num86 < num83 + 3; num86++)
        {
            for (int num87 = num84; num87 < num84 + 2; num87++)
            {
                // SkipWire(num86, num87);
                Main.tile[num86, num87].TileFrameX = (short)(Main.tile[num86, num87].TileFrameX + num85);
            }
        }

        NetMessage.SendTileSquare(-1, num83, num84, 3, 2);
    }
}
