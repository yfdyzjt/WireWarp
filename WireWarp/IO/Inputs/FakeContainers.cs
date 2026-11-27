using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    private static void FakeContainers(int portId, int i, int j)
    {
        int num = Main.tile[i, j].TileFrameX / 18 * -1;
        int num2 = Main.tile[i, j].TileFrameY / 18 * -1;
        num %= 4;
        if (num < -1)
            num += 2;

        num += i;
        num2 += j;
        SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
    }
}
