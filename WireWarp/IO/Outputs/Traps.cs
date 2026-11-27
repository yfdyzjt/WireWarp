using Microsoft.Xna.Framework;
using Terraria;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Traps(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int num95 = tile.TileFrameY / 18;
        Vector2 vector3 = Vector2.Zero;
        float speedX = 0f;
        float speedY = 0f;
        int num96 = 0;
        int damage4 = 0;
        switch (num95)
        {
            case 0:
            case 1:
            case 2:
            case 5:
                if (RuntimeGeneral.CheckMech(i, j, 200))
                {
                    int num104 = ((tile.TileFrameX == 0) ? (-1) : ((tile.TileFrameX == 18) ? 1 : 0));
                    int num105 = ((tile.TileFrameX >= 36) ? ((tile.TileFrameX >= 72) ? 1 : (-1)) : 0);
                    vector3 = new Vector2(i * 16 + 8 + 10 * num104, j * 16 + 8 + 10 * num105);
                    float num106 = 3f;
                    if (num95 == 0)
                    {
                        num96 = 98;
                        damage4 = 20;
                        num106 = 12f;
                    }

                    if (num95 == 1)
                    {
                        num96 = 184;
                        damage4 = 40;
                        num106 = 12f;
                    }

                    if (num95 == 2)
                    {
                        num96 = 187;
                        damage4 = 40;
                        num106 = 5f;
                    }

                    if (num95 == 5)
                    {
                        num96 = 980;
                        damage4 = 30;
                        num106 = 12f;
                    }

                    speedX = (float)num104 * num106;
                    speedY = (float)num105 * num106;
                }
                break;
            case 3:
                if (!RuntimeGeneral.CheckMech(i, j, 300))
                    break;

                int num99 = 200;
                for (int num100 = 0; num100 < 1000; num100++)
                {
                    if (Main.projectile[num100].active && Main.projectile[num100].type == num96)
                    {
                        float num101 = (new Vector2(i * 16 + 8, j * 18 + 8) - Main.projectile[num100].Center).Length();
                        num99 = ((!(num101 < 50f)) ? ((!(num101 < 100f)) ? ((!(num101 < 200f)) ? ((!(num101 < 300f)) ? ((!(num101 < 400f)) ? ((!(num101 < 500f)) ? ((!(num101 < 700f)) ? ((!(num101 < 900f)) ? ((!(num101 < 1200f)) ? (num99 - 1) : (num99 - 2)) : (num99 - 3)) : (num99 - 4)) : (num99 - 5)) : (num99 - 6)) : (num99 - 8)) : (num99 - 10)) : (num99 - 15)) : (num99 - 50));
                    }
                }

                if (num99 > 0)
                {
                    num96 = 185;
                    damage4 = 40;
                    int num102 = 0;
                    int num103 = 0;
                    switch (tile.TileFrameX / 18)
                    {
                        case 0:
                        case 1:
                            num102 = 0;
                            num103 = 1;
                            break;
                        case 2:
                            num102 = 0;
                            num103 = -1;
                            break;
                        case 3:
                            num102 = -1;
                            num103 = 0;
                            break;
                        case 4:
                            num102 = 1;
                            num103 = 0;
                            break;
                    }

                    speedX = (float)(4 * num102) + (float)Main.rand.Next(-20 + ((num102 == 1) ? 20 : 0), 21 - ((num102 == -1) ? 20 : 0)) * 0.05f;
                    speedY = (float)(4 * num103) + (float)Main.rand.Next(-20 + ((num103 == 1) ? 20 : 0), 21 - ((num103 == -1) ? 20 : 0)) * 0.05f;
                    vector3 = new Vector2(i * 16 + 8 + 14 * num102, j * 16 + 8 + 14 * num103);
                }
                break;
            case 4:
                if (RuntimeGeneral.CheckMech(i, j, 90))
                {
                    int num97 = 0;
                    int num98 = 0;
                    switch (tile.TileFrameX / 18)
                    {
                        case 0:
                        case 1:
                            num97 = 0;
                            num98 = 1;
                            break;
                        case 2:
                            num97 = 0;
                            num98 = -1;
                            break;
                        case 3:
                            num97 = -1;
                            num98 = 0;
                            break;
                        case 4:
                            num97 = 1;
                            num98 = 0;
                            break;
                    }

                    speedX = 8 * num97;
                    speedY = 8 * num98;
                    damage4 = 60;
                    num96 = 186;
                    vector3 = new Vector2(i * 16 + 8 + 18 * num97, j * 16 + 8 + 18 * num98);
                }
                break;
        }

        switch (num95)
        {
            case -10:
                if (RuntimeGeneral.CheckMech(i, j, 200))
                {
                    int num111 = -1;
                    if (tile.TileFrameX != 0)
                        num111 = 1;

                    speedX = 12 * num111;
                    damage4 = 20;
                    num96 = 98;
                    vector3 = new Vector2(i * 16 + 8, j * 16 + 7);
                    vector3.X += 10 * num111;
                    vector3.Y += 2f;
                }
                break;
            case -9:
                if (RuntimeGeneral.CheckMech(i, j, 200))
                {
                    int num107 = -1;
                    if (tile.TileFrameX != 0)
                        num107 = 1;

                    speedX = 12 * num107;
                    damage4 = 40;
                    num96 = 184;
                    vector3 = new Vector2(i * 16 + 8, j * 16 + 7);
                    vector3.X += 10 * num107;
                    vector3.Y += 2f;
                }
                break;
            case -8:
                if (RuntimeGeneral.CheckMech(i, j, 200))
                {
                    int num112 = -1;
                    if (tile.TileFrameX != 0)
                        num112 = 1;

                    speedX = 5 * num112;
                    damage4 = 40;
                    num96 = 187;
                    vector3 = new Vector2(i * 16 + 8, j * 16 + 7);
                    vector3.X += 10 * num112;
                    vector3.Y += 2f;
                }
                break;
            case -7:
                if (!RuntimeGeneral.CheckMech(i, j, 300))
                    break;

                num96 = 185;
                int num108 = 200;
                for (int num109 = 0; num109 < 1000; num109++)
                {
                    if (Main.projectile[num109].active && Main.projectile[num109].type == num96)
                    {
                        float num110 = (new Vector2(i * 16 + 8, j * 18 + 8) - Main.projectile[num109].Center).Length();
                        num108 = ((!(num110 < 50f)) ? ((!(num110 < 100f)) ? ((!(num110 < 200f)) ? ((!(num110 < 300f)) ? ((!(num110 < 400f)) ? ((!(num110 < 500f)) ? ((!(num110 < 700f)) ? ((!(num110 < 900f)) ? ((!(num110 < 1200f)) ? (num108 - 1) : (num108 - 2)) : (num108 - 3)) : (num108 - 4)) : (num108 - 5)) : (num108 - 6)) : (num108 - 8)) : (num108 - 10)) : (num108 - 15)) : (num108 - 50));
                    }
                }

                if (num108 > 0)
                {
                    speedX = (float)Main.rand.Next(-20, 21) * 0.05f;
                    speedY = 4f + (float)Main.rand.Next(0, 21) * 0.05f;
                    damage4 = 40;
                    vector3 = new Vector2(i * 16 + 8, j * 16 + 16);
                    vector3.Y += 6f;
                    Projectile.NewProjectile(Wiring.GetProjectileSource(i, j), (int)vector3.X, (int)vector3.Y, speedX, speedY, num96, damage4, 2f, Main.myPlayer);
                }
                break;
            case -6:
                if (RuntimeGeneral.CheckMech(i, j, 90))
                {
                    speedX = 0f;
                    speedY = 8f;
                    damage4 = 60;
                    num96 = 186;
                    vector3 = new Vector2(i * 16 + 8, j * 16 + 16);
                    vector3.Y += 10f;
                }
                break;
        }

        if (num96 != 0)
            Projectile.NewProjectile(Wiring.GetProjectileSource(i, j), (int)vector3.X, (int)vector3.Y, speedX, speedY, num96, damage4, 2f, Main.myPlayer);
    }
}
