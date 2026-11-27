using Terraria;
using Terraria.ID;
using WireWarp.Frontend.Shared;

namespace WireWarp.Frontend.tModLoader.IO;

// The only intentional legacy game-mechanics override. Per-world, opt-in.
internal static class CampfireMode
{
    internal static bool Legacy { get; private set; }
    internal static long Activations { get; private set; }
    internal static long BoundsRejected { get; private set; }
    internal static string Status => $"campfire={(Legacy ? "legacy-1.4.4.9" : "native")} campfireActivations={Activations} campfireBoundsRejected={BoundsRejected}";

    internal static void ResetWorld()
    {
        Legacy = false;
        Activations = BoundsRejected = 0;
    }

    internal static bool TrySetLegacy(bool legacy)
    {
        if (Legacy != legacy && Activations != 0) return false;
        Legacy = legacy;
        return true;
    }

    internal static void RecordActivation() => Activations++;

    // Terraria 1.4.4.9 ToggleCampFire, unforced, doSkipWires=false.
    // Only called for the historical vanilla Campfire tile by RuntimeOutput.
    internal static void ToggleLegacy(int i, int j, Tile tile)
    {
        int x = i - tile.TileFrameX % 54 / 18;
        int y = j - tile.TileFrameY % 36 / 18;
        if (x < 0 || y < 0 || x > Main.maxTilesX - 3 || y > Main.maxTilesY - 2)
        {
            BoundsRejected++;
            if (BoundsRejected <= 8)
                Runtime.DiagnosticSink?.Invoke($"Legacy campfire footprint out of world at ({x},{y})");
            return;
        }

        int delta = Main.tile[x, y].TileFrameY >= 36 ? -36 : 36;
        for (int k = x; k < x + 3; k++)
        for (int l = y; l < y + 2; l++)
        {
            Tile target = Main.tile[k, l];
            // No active/type/footprint validation: mixed cells are intentional.
            // Do not invoke KillTile, TileFrame or SquareTileFrame.
            target.TileFrameY = unchecked((short)(target.TileFrameY + delta));
        }

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, x, y, 3, 2);
    }
    // Same historical behavior, same x-major/y-minor write order, including mixed cells.
    internal static void ToggleLegacyFast(int i, int j, Tile tile)
    {
        int x = i - tile.TileFrameX % 54 / 18;
        int y = j - tile.TileFrameY % 36 / 18;
        if (x < 0 || y < 0 || x > Main.maxTilesX - 3 || y > Main.maxTilesY - 2)
        {
            BoundsRejected++;
            if (BoundsRejected <= 8)
                Runtime.DiagnosticSink?.Invoke($"Legacy campfire footprint out of world at ({x},{y})");
            return;
        }
        int delta = Main.tile[x, y].TileFrameY >= 36 ? -36 : 36;
        FastDeviceOutput.AddFrameY(x, y, delta);
        FastDeviceOutput.AddFrameY(x, y + 1, delta);
        FastDeviceOutput.AddFrameY(x + 1, y, delta);
        FastDeviceOutput.AddFrameY(x + 1, y + 1, delta);
        FastDeviceOutput.AddFrameY(x + 2, y, delta);
        FastDeviceOutput.AddFrameY(x + 2, y + 1, delta);
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, x, y, 3, 2);
    }
}
