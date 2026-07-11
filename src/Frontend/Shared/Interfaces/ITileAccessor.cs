using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.Interfaces;

public interface ITileAccessor
{
    int GetWorldWidth();
    int GetWorldHeight();
    Tile GetTile(int x, int y);
}
