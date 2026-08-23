using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared;

public abstract class Access
{
    public static Access Instance { get; set; } = null!;

    // File

    public abstract string WorldPathName { get; }
    public abstract void SaveWorld(bool resetTime = false, bool useTemps = false, bool canBeSkipped = false);
    public abstract void LoadWorld();

    // Preprocess

    public abstract int MaxTilesX { get; }
    public abstract int MaxTilesY { get; }
    public abstract Tile GetTile(int x, int y);
    public abstract void SetTile(int x, int y, Tile tile);

    // Runtime

    public abstract void Execute(InputID type, int portId, int i, int j);
    public abstract void Execute(OutputID type, int portId, int i, int j);

    public abstract void Tick();
    public abstract void Reset();

    public abstract void Status(string message);
    public abstract void Notify(string message);
}
