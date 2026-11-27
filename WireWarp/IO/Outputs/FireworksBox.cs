using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void FireworksBox(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num72 = j - tile.TileFrameY / 18;
        int num73 = i - tile.TileFrameX / 18;
        // SkipWire(num73, num72);
        // SkipWire(num73, num72 + 1);
        // SkipWire(num73 + 1, num72);
        // SkipWire(num73 + 1, num72 + 1);
        if (RuntimeGeneral.CheckMech(num73, num72, 30))
        {
            WorldGen.LaunchRocketSmall(num73, num72, fromWiring: true);
        }
    }
}
