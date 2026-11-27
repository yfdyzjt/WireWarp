using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void CannonsShot(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num31 = tile.TileFrameX % 72 / 18;
        int num32 = tile.TileFrameY % 54 / 18;
        int num33 = i - num31;
        int num34 = j - num32;
        int num35 = tile.TileFrameY / 54;
        int num36 = tile.TileFrameX / 72;
        int num37 = num32;
        bool flag2 = true;
        if ((num36 == 3 || num36 == 4) && num37 < 2)
            flag2 = false;

        int damage = 0;
        float knockBack = 0f;
        int time = 30;
        switch (num36)
        {
            case 0:
                if (RuntimeGeneral.cannonCoolDown > 0)
                    return;
                damage = 300;
                knockBack = 8f;
                time = 480;
                break;
            case 1:
                if (RuntimeGeneral.bunnyCannonCoolDown > 0)
                    return;
                damage = 350;
                knockBack = 8f;
                time = 3600;
                break;
        }

        if (RuntimeGeneral.CheckMech(num33, num34, time) && flag2)
        {
            switch (num36)
            {
                case 0:
                    RuntimeGeneral.cannonCoolDown = 120;
                    break;
                case 1:
                    RuntimeGeneral.bunnyCannonCoolDown = 480;
                    break;
            }

            WorldGen.ShootFromCannon(num33, num34, num35, num36 + 1, damage, knockBack, Main.myPlayer, fromWire: true);
        }
    }
}
