using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.Interfaces;

public interface ITerraria
{
    int MaxTilesX { get; }
    int MaxTilesY { get; }
    Tile Tile(int x, int y);
}
