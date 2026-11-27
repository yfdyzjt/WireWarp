using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Lamps(int portId, int i, int j)
        => Wiring.ToggleLamp(i, j, Main.tile[i, j], null, false);
}
