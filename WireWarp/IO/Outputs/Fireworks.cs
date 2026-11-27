using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Fireworks(int portId, int i, int j)
    {
        WorldGen.LaunchRocket(i, j, fromWiring: true);
        // SkipWire(i, j);
    }
}
