using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Toilets(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.TileType;
        if (type == TileID.Toilets || (type == TileID.Chairs && tile.TileFrameY / 40 == 1) || (type == TileID.Chairs && tile.TileFrameY / 40 == 20))
        {
            int num68 = j - tile.TileFrameY % 40 / 18;
            // SkipWire(i, num68);
            // SkipWire(i, num68 + 1);
            if (RuntimeGeneral.CheckMech(i, num68, 60))
            {
                Projectile.NewProjectile(Wiring.GetProjectileSource(i, num68), i * 16 + 8, num68 * 16 + 12, 0f, 0f, 733, 0, 0f, Main.myPlayer);
            }
        }
    }
}
