using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Torches(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (TileID.Sets.Torches[tile.TileType])
            Wiring.ToggleTorch(i, j, tile, null);
    }
}
