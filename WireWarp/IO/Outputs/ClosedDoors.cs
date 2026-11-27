using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void ClosedDoors(int portId, int i, int j)
    {
        int num67 = 1;
        if (Main.rand.Next(2) == 0)
            num67 = -1;

        if (!WorldGen.OpenDoor(i, j, num67))
        {
            if (WorldGen.OpenDoor(i, j, -num67))
                NetMessage.SendData(/*19*/MessageID.ToggleDoorState, -1, -1, /*null,*/ number: 0, number2: i, number3: j, number4: -num67);
        }
        else
        {
            NetMessage.SendData(/*19*/MessageID.ToggleDoorState, -1, -1, /*null,*/ number: 0, number2: i, number3: j, number4: num67);
        }
    }
}
