using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Monoliths(int portId, int i, int j)
        => WorldGen.SwitchMonolith(i, j);
}
