using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void AnnouncementBox(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num5 = tile.TileFrameX % 36 / 18;
        int num6 = tile.TileFrameY % 36 / 18;
        int num7 = i - num5;
        int num8 = j - num6;
        // for (int m = num7; m < num7 + 2; m++)
        // {
        //     for (int n = num8; n < num8 + 2; n++)
        //     {
        //         SkipWire(m, n);
        //     }
        // }

        if (Main.AnnouncementBoxDisabled)
            return;

        Color pink = Color.Pink;
        int num9 = Sign.ReadSign(num7, num8, CreateIfMissing: false);
        if (num9 == -1 || Main.sign[num9] == null || string.IsNullOrWhiteSpace(Main.sign[num9].text))
            return;

        if (Main.AnnouncementBoxRange == -1)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewTextMultiline(Main.sign[num9].text, force: false, pink, 460);
            else if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(107, -1, -1, NetworkText.FromLiteral(Main.sign[num9].text), 255, (int)pink.R, (int)pink.G, (int)pink.B, 460);
        }
        else if (Main.netMode == NetmodeID.SinglePlayer)
        {
            if (Main.player[Main.myPlayer].Distance(new Vector2(num7 * 16 + 16, num8 * 16 + 16)) <= (float)Main.AnnouncementBoxRange)
                Main.NewTextMultiline(Main.sign[num9].text, force: false, pink, 460);
        }
        else
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            for (int num10 = 0; num10 < 255; num10++)
            {
                if (Main.player[num10].active && Main.player[num10].Distance(new Vector2(num7 * 16 + 16, num8 * 16 + 16)) <= (float)Main.AnnouncementBoxRange)
                    NetMessage.SendData(107, num10, -1, NetworkText.FromLiteral(Main.sign[num9].text), 255, (int)pink.R, (int)pink.G, (int)pink.B, 460);
            }
        }
    }
}
