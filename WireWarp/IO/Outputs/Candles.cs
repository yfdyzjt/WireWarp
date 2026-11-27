using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Candles(int portId, int i, int j)
        => Wiring.ToggleCandle(i, j, Main.tile[i, j], null);
}
