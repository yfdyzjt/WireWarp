using Microsoft.Xna.Framework;
using Terraria;


namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void SnowballLauncherShot(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num51 = tile.TileFrameX % 54 / 18;
        int num52 = tile.TileFrameY % 54 / 18;
        int num53 = i - num51;
        int num54 = j - num52;
        int num56 = num52;
        if (num56 != -1 && RuntimeGeneral.snowballCannonCoolDown == 0 && RuntimeGeneral.CheckMech(num53, num54, 60))
        {
            RuntimeGeneral.snowballCannonCoolDown = 15;
            float num60 = 12f + (float)Main.rand.Next(450) * 0.01f;
            float num61 = Main.rand.Next(85, 105);
            float num62 = Main.rand.Next(-35, 11);
            int type2 = 166;
            int damage2 = 35;
            float knockBack2 = 3.5f;
            Vector2 vector = new Vector2((num53 + 2) * 16 - 8, (num54 + 2) * 16 - 8);
            if (tile.TileFrameX / 54 == 0)
            {
                num61 *= -1f;
                vector.X -= 12f;
            }
            else
            {
                vector.X += 12f;
            }

            float num63 = num61;
            float num64 = num62;
            float num65 = (float)Math.Sqrt(num63 * num63 + num64 * num64);
            num65 = num60 / num65;
            num63 *= num65;
            num64 *= num65;
            Projectile.NewProjectile(Wiring.GetProjectileSource(num53, num54), vector.X, vector.Y, num63, num64, type2, damage2, knockBack2, Main.myPlayer);
        }
    }
}
