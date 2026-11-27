using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Campfires(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (!TileID.Sets.Campfires[tile.TileType]) return;
        if (!tile.HasTile) return;

        CampfireMode.RecordActivation();
        if (CampfireMode.Legacy && tile.TileType == TileID.Campfire)
        {
            CampfireMode.ToggleLegacy(i, j, tile);
            return;
        }

        // Default: use the installed modern Terraria implementation verbatim.
        Wiring.ToggleCampFire(i, j, tile, null, false);
    }
}
