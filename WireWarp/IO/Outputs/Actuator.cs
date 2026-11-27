using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Actuator(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (tile.HasActuator)
            Wiring.ActuateForced(i, j);
    }
}
