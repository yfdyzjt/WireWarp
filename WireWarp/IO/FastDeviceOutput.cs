#nullable enable annotations
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.tModLoader.IO;

// Opt-in static handler selection. No caching of Tile, tile frame, origin, or world state.
internal static class FastDeviceOutput
{
    internal static Action<int, int, int>? Resolve(OutputID type) => type switch
    {
        OutputID.Chimney => Chimney,
        OutputID.Campfires => Campfires,
        _ => RuntimeOutput.ResolveHandler(type),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AddFrameY(int x, int y, int delta)
    {
        Tile target = Main.tile[x, y];
        target.TileFrameY = unchecked((short)(target.TileFrameY + delta));
    }

    private static void Chimney(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int x = i - tile.TileFrameX % 54 / 18;
        int y = j - tile.TileFrameY % 54 / 18;
        int delta = Main.tile[x, y].TileFrameY >= 108 ? -108 : 54;
        // Same footprint and exact x-major order as the reference. Do not validate mixed tiles.
        AddFrameY(x, y, delta);
        AddFrameY(x, y + 1, delta);
        AddFrameY(x, y + 2, delta);
        AddFrameY(x + 1, y, delta);
        AddFrameY(x + 1, y + 1, delta);
        AddFrameY(x + 1, y + 2, delta);
        AddFrameY(x + 2, y, delta);
        AddFrameY(x + 2, y + 1, delta);
        AddFrameY(x + 2, y + 2, delta);
        // Retain the exact existing call, even in single player (possible mod hooks).
        NetMessage.SendTileSquare(-1, x + 1, y + 1, 3);
    }

    private static void Campfires(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (!TileID.Sets.Campfires[tile.TileType]) return;
        if (!tile.HasTile) return;
        CampfireMode.RecordActivation();
        if (CampfireMode.Legacy && tile.TileType == TileID.Campfire)
        {
            CampfireMode.ToggleLegacyFast(i, j, tile);
            return;
        }
        // Preserve installed modern Terraria behavior without a local reimplementation.
        Wiring.ToggleCampFire(i, j, tile, null, false);
    }
}
