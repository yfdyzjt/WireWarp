using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void PortalGunStationChange(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num31 = tile.TileFrameX % 72 / 18;
        int num32 = tile.TileFrameY % 54 / 18;
        int num33 = i - num31;
        int num34 = j - num32;
        int num36 = tile.TileFrameX / 72;
        int num37 = num32;
        int num38 = (num36 == 3) ? 72 : (-72);
        for (int num41 = num33; num41 < num33 + 4; num41++)
        {
            for (int num42 = num34; num42 < num34 + 3; num42++)
            {
                // SkipWire(num41, num42);
                Main.tile[num41, num42].TileFrameX = (short)(Main.tile[num41, num42].TileFrameX + num38);
            }
        }

        NetMessage.SendTileSquare(-1, num33, num34, 4, 3);
    }
}
