using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void HolidayLights(int portId, int i, int j)
        => Wiring.ToggleHolidayLight(i, j, Main.tile[i, j], null);
}
