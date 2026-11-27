using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Explosives(int portId, int i, int j)
    {
        WorldGen.KillTile(i, j, fail: false, effectOnly: false, noItem: true);
        NetMessage.SendTileSquare(-1, i, j);
        Projectile.NewProjectile(Wiring.GetProjectileSource(i, j), i * 16 + 8, j * 16 + 8, 0f, 0f, 108, 500, 10f, Main.myPlayer);
    }
}
