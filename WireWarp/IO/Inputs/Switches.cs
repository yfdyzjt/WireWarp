using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    private static void Switches(int portId, int i, int j)
    {
        if (Main.tile[i, j].TileFrameY == 0)
            Main.tile[i, j].TileFrameY = 18;
        else
            Main.tile[i, j].TileFrameY = 0;

        SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
    }
}
