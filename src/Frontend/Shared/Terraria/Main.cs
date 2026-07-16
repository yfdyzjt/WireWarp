using WireWarp.Frontend.Shared.Interfaces;

namespace WireWarp.Frontend.Shared.Terraria;

internal static class Main
{
    internal static ITerraria Terraria = null!;
    internal static int maxTilesX;
    internal static int maxTilesY;

    internal static Tile tile(int x, int y) => Terraria.Tile(x, y);
}
