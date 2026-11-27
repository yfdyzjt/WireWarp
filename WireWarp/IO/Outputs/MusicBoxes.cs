using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void MusicBoxes(int portId, int i, int j)
        => WorldGen.SwitchMB(i, j);
}
