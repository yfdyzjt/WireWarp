using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void SunAndMoondial(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.TileType;
        if (type == TileID.Sundial)
        {
            int num = tile.TileFrameX % 36 / 18;
            int num2 = tile.TileFrameY % 54 / 18;
            int num3 = i - num;
            int num4 = j - num2;
            // for (int k = num3; k < num3 + 2; k++)
            // {
            //     for (int l = num4; l < num4 + 3; l++)
            //     {
            //         SkipWire(k, l);
            //     }
            // }

            if (!Main.fastForwardTimeToDawn && Main.sundialCooldown == 0)
                Main.Sundialing();

            NetMessage.SendTileSquare(-1, num3, num4, 2, 2);
        }
        else if (type == TileID.Moondial)
        {
            int num25 = tile.TileFrameX % 36 / 18;
            int num26 = tile.TileFrameY % 54 / 18;
            int num27 = i - num25;
            int num28 = j - num26;
            // for (int num29 = num27; num29 < num27 + 2; num29++)
            // {
            //     for (int num30 = num28; num30 < num28 + 3; num30++)
            //     {
            //         SkipWire(num29, num30);
            //     }
            // }

            if (!Main.fastForwardTimeToDusk && Main.moondialCooldown == 0)
                Main.Moondialing();

            NetMessage.SendTileSquare(-1, num27, num28, 2, 2);
        }
    }
}
