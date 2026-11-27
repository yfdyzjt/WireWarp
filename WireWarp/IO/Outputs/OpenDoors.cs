using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void OpenDoors(int portId, int i, int j)
    {
        if (WorldGen.CloseDoor(i, j, forced: true))
            NetMessage.SendData(/*19*/MessageID.ToggleDoorState, -1, -1, /*null,*/ number: 1, number2: i, number3: j);
    }
}
