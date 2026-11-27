using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Timers(int portId, int i, int j)
    {
        RuntimeInput.Timers(portId, i, j);
        WorldGen.SquareTileFrame(i, j);
        NetMessage.SendTileSquare(-1, i, j);
    }
}
