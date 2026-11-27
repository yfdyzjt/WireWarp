using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void HangingLanterns(int portId, int i, int j)
        => Wiring.ToggleHangingLantern(i, j, Main.tile[i, j], null, false);
}
