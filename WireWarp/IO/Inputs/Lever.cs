using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    private static void Lever(int portId, int i, int j)
    {
        short num5 = 36;
        int num6 = Main.tile[i, j].TileFrameX / 18 * -1;
        int num7 = Main.tile[i, j].TileFrameY / 18 * -1;
        num6 %= 4;
        if (num6 < -1)
        {
            num6 += 2;
            num5 = -36;
        }

        num6 += i;
        num7 += j;
        if (Main.netMode != NetmodeID.MultiplayerClient && Main.tile[num6, num7].TileType == TileID.Detonator)
        {
            RuntimeGeneral.CheckMech(num6, num7, 60);
        }

        for (int k = num6; k < num6 + 2; k++)
        {
            for (int l = num7; l < num7 + 2; l++)
            {
                if (Main.tile[k, l].TileType == TileID.Lever || Main.tile[k, l].TileType == TileID.Detonator)
                    Main.tile[k, l].TileFrameX += num5;
            }
        }

        WorldGen.TileFrame(num6, num7);
        SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
    }
}
