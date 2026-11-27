using Terraria;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Pumps(int portId, int i, int j)
    {
        if (!IOExtra.Pumps.TryGetValue(portId, out var pair)) return;

        int[] inPumpX = new int[20];
        int[] inPumpY = new int[20];
        int[] outPumpX = new int[20];
        int[] outPumpY = new int[20];
        int numInPump = FillPumps(pair.inlets, inPumpX, inPumpY);
        int numOutPump = FillPumps(pair.outlets, outPumpX, outPumpY);

        if (numInPump > 0 && numOutPump > 0)
            XferWater(inPumpX, inPumpY, numInPump, outPumpX, outPumpY, numOutPump);
    }

    private static int FillPumps(List<(int x, int y)> actives, int[] pumpX, int[] pumpY)
    {
        const int MaxPump = 19;

        int count = 0;
        foreach (var (x, y) in actives)
        {
            Tile tile = Main.tile[x, y];
            int num119 = y - tile.TileFrameY / 18;
            int num120 = tile.TileFrameX / 18;
            if (num120 > 1)
                num120 -= 2;

            num120 = x - num120;
            for (int num121 = 0; num121 < 4; num121++)
            {
                if (count >= MaxPump)
                    break;

                int num122;
                int num123;
                switch (num121)
                {
                    case 0:
                        num122 = num120;
                        num123 = num119 + 1;
                        break;
                    case 1:
                        num122 = num120 + 1;
                        num123 = num119 + 1;
                        break;
                    case 2:
                        num122 = num120;
                        num123 = num119;
                        break;
                    default:
                        num122 = num120 + 1;
                        num123 = num119;
                        break;
                }

                pumpX[count] = num122;
                pumpY[count] = num123;
                count++;
            }
        }

        return count;
    }

    private static void XferWater(int[] inPumpX, int[] inPumpY, int numInPump, int[] outPumpX, int[] outPumpY, int numOutPump)
    {
        for (int i = 0; i < numInPump; i++)
        {
            int num = inPumpX[i];
            int num2 = inPumpY[i];
            int liquid = Main.tile[num, num2].LiquidAmount;
            if (liquid <= 0)
                continue;

            byte b = (byte)Main.tile[num, num2].LiquidType;
            for (int j = 0; j < numOutPump; j++)
            {
                int num3 = outPumpX[j];
                int num4 = outPumpY[j];
                int liquid2 = Main.tile[num3, num4].LiquidAmount;
                if (liquid2 >= 255)
                    continue;

                byte b2 = (byte)Main.tile[num3, num4].LiquidType;
                if (liquid2 == 0)
                    b2 = b;

                if (b2 == b)
                {
                    int num5 = liquid;
                    if (num5 + liquid2 > 255)
                        num5 = 255 - liquid2;

                    Tile outTile = Main.tile[num3, num4];
                    outTile.LiquidAmount = (byte)(outTile.LiquidAmount + num5);
                    Tile inTile = Main.tile[num, num2];
                    inTile.LiquidAmount = (byte)(inTile.LiquidAmount - num5);
                    liquid = inTile.LiquidAmount;
                    outTile.LiquidType = b;
                    WorldGen.SquareTileFrame(num3, num4);
                    if (inTile.LiquidAmount == 0)
                    {
                        inTile.LiquidType = 0;
                        WorldGen.SquareTileFrame(num, num2);
                        break;
                    }
                }
            }

            WorldGen.SquareTileFrame(num, num2);
        }
    }
}
