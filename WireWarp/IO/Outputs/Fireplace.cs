using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Fireplace(int portId, int i, int j)
        => Wiring.ToggleFirePlace(i, j, Main.tile[i, j], null, false);
}
