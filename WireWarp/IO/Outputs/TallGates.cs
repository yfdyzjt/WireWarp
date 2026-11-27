using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void TallGates(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        bool flag4 = tile.TileType == TileID.TallGateOpen;
        WorldGen.ShiftTallGate(i, j, flag4);
        NetMessage.SendData(/*19*/MessageID.ToggleDoorState, -1, -1, /*null,*/ number: 4 + flag4.ToInt(), number2: i, number3: j);
    }
}
