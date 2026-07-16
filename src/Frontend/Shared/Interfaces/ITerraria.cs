using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.Interfaces;

public interface ITerraria
{
    int MaxTilesX { get; }
    int MaxTilesY { get; }
    Tile GetTile(int x, int y);
}
