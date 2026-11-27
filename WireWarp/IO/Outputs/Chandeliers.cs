using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Chandeliers(int portId, int i, int j)
        => Wiring.ToggleChandelier(i, j, Main.tile[i, j], null, false);
}
