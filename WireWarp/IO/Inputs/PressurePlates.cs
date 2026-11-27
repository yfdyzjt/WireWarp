using Microsoft.Xna.Framework;
using Terraria.Audio;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    private static void PressurePlates(int portId, int i, int j)
    {
        SoundEngine.PlaySound(SoundID.Mech, new Vector2(i * 16, j * 16));
    }
}
