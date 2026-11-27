using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void TrapdoorOpen(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        bool value = tile.TileType == TileID.TrapdoorClosed;
        int num66 = WorldGen.ShiftTrapdoor(i, j, playerAbove: true).ToInt();
        if (num66 == 0)
            num66 = -WorldGen.ShiftTrapdoor(i, j, playerAbove: false).ToInt();

        if (num66 != 0)
            NetMessage.SendData(/*19*/MessageID.ToggleDoorState, -1, -1, /*null,*/ number: 3 - value.ToInt(), number2: i, number3: j, number4: num66);
    }
}
