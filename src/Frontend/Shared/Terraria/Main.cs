using WireWarp.Frontend.Shared.Interfaces;

namespace WireWarp.Frontend.Shared.Terraria;

public static class Main
{
    public static TileArray tile = null!;
    public static int maxTilesX;
    public static int maxTilesY;

    public sealed class TileArray
    {
        private readonly ITerraria _terraria;

        internal TileArray(ITerraria terraria)
        {
            _terraria = terraria;
        }

        public Tile this[int x, int y]
        {
            get => _terraria.GetTile(x, y);
        }
    }
}
