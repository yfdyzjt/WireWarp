using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void MinecartTrack(int portId, int i, int j)
    {
        if (RuntimeGeneral.CheckMech(i, j, 5))
        {
            Minecart.FlipSwitchTrack(i, j);
        }
    }
}
