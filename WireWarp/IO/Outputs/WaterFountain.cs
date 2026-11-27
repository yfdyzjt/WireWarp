using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void WaterFountain(int portId, int i, int j)
        => WorldGen.SwitchFountain(i, j);
}
