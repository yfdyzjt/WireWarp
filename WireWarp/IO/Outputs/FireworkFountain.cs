using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void FireworkFountain(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num69 = j - tile.TileFrameY / 18;
        int num70 = i - tile.TileFrameX / 18;
        // SkipWire(num70, num69);
        // SkipWire(num70, num69 + 1);
        if (RuntimeGeneral.CheckMech(num70, num69, 30))
        {
            bool flag5 = false;
            for (int num71 = 0; num71 < 1000; num71++)
            {
                if (Main.projectile[num71].active && Main.projectile[num71].aiStyle == 73 && Main.projectile[num71].ai[0] == (float)num70 && Main.projectile[num71].ai[1] == (float)num69)
                {
                    flag5 = true;
                    break;
                }
            }

            if (!flag5)
            {
                int type3 = 419 + Main.rand.Next(4);
                Projectile.NewProjectile(Wiring.GetProjectileSource(num70, num69), num70 * 16 + 8, num69 * 16 + 2, 0f, 0f, type3, 0, 0f, Main.myPlayer, num70, num69);
            }
        }
    }
}
