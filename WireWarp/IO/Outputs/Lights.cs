using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Lights(int portId, int i, int j)
        => Wiring.Toggle2x2Light(i, j, Main.tile[i, j], null, false);
}
