using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    internal static void GeyserTrap(int portId, int i, int j)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Tile tile = Main.tile[i, j];
        if (tile.TileType != TileID.GeyserTrap)
            return;

        int num = tile.TileFrameX / 36;
        int num2 = i - (tile.TileFrameX - num * 36) / 18;
        if (RuntimeGeneral.CheckMech(num2, j, 200))
        {
            Vector2 zero = Vector2.Zero;
            Vector2 zero2 = Vector2.Zero;
            int num3 = 654;
            int damage = 20;
            if (num < 2)
            {
                zero = new Vector2(num2 + 1, j) * 16f;
                zero2 = new Vector2(0f, -8f);
            }
            else
            {
                zero = new Vector2(num2 + 1, j + 1) * 16f;
                zero2 = new Vector2(0f, 8f);
            }

            if (num3 != 0)
                Projectile.NewProjectile(new EntitySource_Wiring(num2, j), (int)zero.X, (int)zero.Y, zero2.X, zero2.Y, num3, damage, 2f, Main.myPlayer);
        }
    }
}
