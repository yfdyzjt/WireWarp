using Terraria;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void WireBulb(int portId, int i, int j)
    {
        if (!IOExtra.WireBulb.TryGetValue(portId, out var wireType)) return;

        Tile tile = Main.tile[i, j];
        int num156 = tile.TileFrameX / 18;
        bool flag8 = num156 % 2 >= 1;
        bool flag9 = num156 % 4 >= 2;
        bool flag10 = num156 % 8 >= 4;
        bool flag11 = num156 % 16 >= 8;
        bool flag12 = false;
        short num157 = 0;
        switch (wireType)
        {
            case WireID.Red:
                num157 = 18;
                flag12 = !flag8;
                break;
            case WireID.Blue:
                num157 = 72;
                flag12 = !flag10;
                break;
            case WireID.Green:
                num157 = 36;
                flag12 = !flag9;
                break;
            case WireID.Yellow:
                num157 = 144;
                flag12 = !flag11;
                break;
        }

        if (flag12)
            tile.TileFrameX += num157;
        else
            tile.TileFrameX -= num157;

        NetMessage.SendTileSquare(-1, i, j);
    }
}
