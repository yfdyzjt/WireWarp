using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    private static void DeadMansChest(int portId, int i, int j)
    {
        if (Main.tile[i, j].TileFrameX / 36 == 4)
        {
            int num3 = Main.tile[i, j].TileFrameX / 18 * -1;
            int num4 = Main.tile[i, j].TileFrameY / 18 * -1;
            num3 %= 4;
            if (num3 < -1)
                num3 += 2;

            num3 += i;
            num4 += j;
            SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
        }
    }
}
