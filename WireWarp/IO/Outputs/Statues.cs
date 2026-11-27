using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Statues(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        int type = tile.TileType;
        switch (type)
        {
            case TileID.BoulderStatue:
                {
                    int num88 = tile.TileFrameX / 36;
                    int num89 = tile.TileFrameY / 54;
                    int num90 = i - (tile.TileFrameX - num88 * 36) / 18;
                    int num91 = j - (tile.TileFrameY - num89 * 54) / 18;
                    if (RuntimeGeneral.CheckMech(num90, num91, 900))
                    {
                        Vector2 vector2 = new Vector2(num90 + 1, num91) * 16f;
                        vector2.Y += 28f;
                        int num92 = 99;
                        int damage3 = 70;
                        float knockBack3 = 10f;
                        if (num92 != 0)
                            Projectile.NewProjectile(Wiring.GetProjectileSource(num90, num91), (int)vector2.X, (int)vector2.Y, 0f, 0f, num92, damage3, knockBack3, Main.myPlayer);
                    }
                }
                break;
            case TileID.Statues:
                {
                    int num138 = j - tile.TileFrameY / 18;
                    int num139 = tile.TileFrameX / 18;
                    int num140 = 0;
                    while (num139 >= 2)
                    {
                        num139 -= 2;
                        num140++;
                    }

                    num139 = i - num139;
                    num139 = i - tile.TileFrameX % 36 / 18;
                    num138 = j - tile.TileFrameY % 54 / 18;
                    int num141 = tile.TileFrameY / 54;
                    num141 %= 3;
                    num140 = tile.TileFrameX / 36 + num141 * 55;
                    // SkipWire(num139, num138);
                    // SkipWire(num139, num138 + 1);
                    // SkipWire(num139, num138 + 2);
                    // SkipWire(num139 + 1, num138);
                    // SkipWire(num139 + 1, num138 + 1);
                    // SkipWire(num139 + 1, num138 + 2);
                    int num142 = num139 * 16 + 16;
                    int num143 = (num138 + 3) * 16;
                    int num144 = -1;
                    int num145 = -1;
                    bool flag6 = true;
                    bool flag7 = false;
                    switch (num140)
                    {
                        case 5:
                            num145 = 73;
                            break;
                        case 13:
                            num145 = 24;
                            break;
                        case 30:
                            num145 = 6;
                            break;
                        case 35:
                            num145 = 2;
                            break;
                        case 51:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                299,
                                538
                            });
                            break;
                        case 52:
                            num145 = 356;
                            break;
                        case 53:
                            num145 = 357;
                            break;
                        case 54:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                355,
                                358
                            });
                            break;
                        case 55:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                367,
                                366
                            });
                            break;
                        case 56:
                            num145 = Utils.SelectRandom(Main.rand, new short[5] {
                                359,
                                359,
                                359,
                                359,
                                360
                            });
                            break;
                        case 57:
                            num145 = 377;
                            break;
                        case 58:
                            num145 = 300;
                            break;
                        case 59:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                364,
                                362
                            });
                            break;
                        case 60:
                            num145 = 148;
                            break;
                        case 61:
                            num145 = 361;
                            break;
                        case 62:
                            num145 = Utils.SelectRandom(Main.rand, new short[3] {
                                487,
                                486,
                                485
                            });
                            break;
                        case 63:
                            num145 = 164;
                            flag6 &= NPC.MechSpawn(num142, num143, 165);
                            break;
                        case 64:
                            num145 = 86;
                            flag7 = true;
                            break;
                        case 65:
                            num145 = 490;
                            break;
                        case 66:
                            num145 = 82;
                            break;
                        case 67:
                            num145 = 449;
                            break;
                        case 68:
                            num145 = 167;
                            break;
                        case 69:
                            num145 = 480;
                            break;
                        case 70:
                            num145 = 48;
                            break;
                        case 71:
                            num145 = Utils.SelectRandom(Main.rand, new short[3] {
                                170,
                                180,
                                171
                            });
                            flag7 = true;
                            break;
                        case 72:
                            num145 = 481;
                            break;
                        case 73:
                            num145 = 482;
                            break;
                        case 74:
                            num145 = 430;
                            break;
                        case 75:
                            num145 = 489;
                            break;
                        case 76:
                            num145 = 611;
                            break;
                        case 77:
                            num145 = 602;
                            break;
                        case 78:
                            num145 = Utils.SelectRandom(Main.rand, new short[6] {
                                595,
                                596,
                                599,
                                597,
                                600,
                                598
                            });
                            break;
                        case 79:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                616,
                                617
                            });
                            break;
                        case 80:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                671,
                                672
                            });
                            break;
                        case 81:
                            num145 = 673;
                            break;
                        case 82:
                            num145 = Utils.SelectRandom(Main.rand, new short[2] {
                                674,
                                675
                            });
                            break;
                    }

                    if (num145 != -1 && RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, num145) && flag6)
                    if (num145 != -1 && NPC.MechSpawn(num142, num143, num145) && flag6)
                    {
                        if (!flag7 || !Collision.SolidTiles(num139 - 2, num139 + 3, num138, num138 + 2))
                        {
                            num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143, num145);
                        }
                        else
                        {
                            Vector2 position = new Vector2(num142 - 4, num143 - 22) - new Vector2(10f);
                            Utils.PoofOfSmoke(position);
                            NetMessage.SendData(/*106*/MessageID.PoofOfSmoke, -1, -1, /*null,*/ number: (int)position.X, number2: position.Y);
                        }
                    }

                    if (num144 <= -1)
                    {
                        switch (num140)
                        {
                            case 4:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 1))
                                if (NPC.MechSpawn(num142, num143, 1))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 1);
                                break;
                            case 7:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 49))
                                if (NPC.MechSpawn(num142, num143, 49))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142 - 4, num143 - 6, 49);
                                break;
                            case 8:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 55))
                                if (NPC.MechSpawn(num142, num143, 55))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 55);
                                break;
                            case 9:
                                {
                                    int type4 = 46;
                                    if (BirthdayParty.PartyIsUp)
                                        type4 = 540;

                                    if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, type4))
                                    if (NPC.MechSpawn(num142, num143, type4))
                                        num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, type4);
                                }
                                break;
                            case 10:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 21))
                                if (NPC.MechSpawn(num142, num143, 21))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143, 21);
                                break;
                            case 16:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 42))
                                if (NPC.MechSpawn(num142, num143, 42))
                                {
                                    if (!Collision.SolidTiles(num139 - 1, num139 + 1, num138, num138 + 1))
                                    {
                                        num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 42);
                                        break;
                                    }

                                    Vector2 position3 = new Vector2(num142 - 4, num143 - 22) - new Vector2(10f);
                                    Utils.PoofOfSmoke(position3);
                                    NetMessage.SendData(/*106*/MessageID.PoofOfSmoke, -1, -1, /*null,*/ number: (int)position3.X, number2: position3.Y);
                                }
                                break;
                            case 18:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 67))
                                if (NPC.MechSpawn(num142, num143, 67))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 67);
                                break;
                            case 23:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 63))
                                if (NPC.MechSpawn(num142, num143, 63))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 63);
                                break;
                            case 27:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 85))
                                if (NPC.MechSpawn(num142, num143, 85))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142 - 9, num143, 85);
                                break;
                            case 28:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 74))
                                if (NPC.MechSpawn(num142, num143, 74))
                                {
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, Utils.SelectRandom(Main.rand, new short[3] {
                                        74,
                                        297,
                                        298
                                    }));
                                }
                                break;
                            case 34:
                                {
                                    for (int num154 = 0; num154 < 2; num154++)
                                    {
                                        for (int num155 = 0; num155 < 3; num155++)
                                        {
                                            Tile tile2 = Main.tile[num139 + num154, num138 + num155];
                                            tile2.TileType = TileID.MushroomStatue;
                                            tile2.TileFrameX = (short)(num154 * 18 + 216);
                                            tile2.TileFrameY = (short)(num155 * 18);
                                        }
                                    }

                                    Animation.NewTemporaryAnimation(0, TileID.MushroomStatue, num139, num138);
                                    if (Main.netMode == NetmodeID.Server)
                                        NetMessage.SendTileSquare(-1, num139, num138, 2, 3);
                                }
                                break;
                            case 42:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 58))
                                if (NPC.MechSpawn(num142, num143, 58))
                                    num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 58);
                                break;
                            case 37:
                                if (RuntimeGeneral.CheckMech(num139, num138, 600) && Item.MechSpawn(num142, num143, 58) && Item.MechSpawn(num142, num143, 1734) && Item.MechSpawn(num142, num143, 1867))
                                if (Item.MechSpawn(num142, num143, 58) && Item.MechSpawn(num142, num143, 1734) && Item.MechSpawn(num142, num143, 1867))
                                    Item.NewItem(Wiring.GetItemSource(num142, num143), num142, num143 - 16, 0, 0, 58);
                                break;
                            case 50:
                                if (RuntimeGeneral.CheckMech(num139, num138, 30) && NPC.MechSpawn(num142, num143, 65))
                                if (NPC.MechSpawn(num142, num143, 65))
                                {
                                    if (!Collision.SolidTiles(num139 - 2, num139 + 3, num138, num138 + 2))
                                    {
                                        num144 = NPC.NewNPC(Wiring.GetNPCSource(num139, num138), num142, num143 - 12, 65);
                                        break;
                                    }

                                    Vector2 position2 = new Vector2(num142 - 4, num143 - 22) - new Vector2(10f);
                                    Utils.PoofOfSmoke(position2);
                                    NetMessage.SendData(/*106*/MessageID.PoofOfSmoke, -1, -1, /*null,*/ number: (int)position2.X, number2: position2.Y);
                                }
                                break;
                            case 2:
                                if (RuntimeGeneral.CheckMech(num139, num138, 600) && Item.MechSpawn(num142, num143, 184) && Item.MechSpawn(num142, num143, 1735) && Item.MechSpawn(num142, num143, 1868))
                                if (Item.MechSpawn(num142, num143, 184) && Item.MechSpawn(num142, num143, 1735) && Item.MechSpawn(num142, num143, 1868))
                                    Item.NewItem(Wiring.GetItemSource(num142, num143), num142, num143 - 16, 0, 0, 184);
                                break;
                            case 17:
                                if (RuntimeGeneral.CheckMech(num139, num138, 600) && Item.MechSpawn(num142, num143, 166))
                                if (Item.MechSpawn(num142, num143, 166))
                                    Item.NewItem(Wiring.GetItemSource(num142, num143), num142, num143 - 20, 0, 0, 166);
                                break;
                            case 40:
                                {
                                    if (!RuntimeGeneral.CheckMech(num139, num138, 300))
                                    {
                                        int num150 = 50;
                                        int[] array2 = new int[num150];
                                        int num151 = 0;
                                        for (int num152 = 0; num152 < Main.maxNPCs; num152++)
                                        {
                                            if (Main.npc[num152].active && (Main.npc[num152].type == 17 || Main.npc[num152].type == 19 || Main.npc[num152].type == 22 || Main.npc[num152].type == 38 || Main.npc[num152].type == 54 || Main.npc[num152].type == 107 || Main.npc[num152].type == 108 || Main.npc[num152].type == 142 || Main.npc[num152].type == 160 || Main.npc[num152].type == 207 || Main.npc[num152].type == 209 || Main.npc[num152].type == 227 || Main.npc[num152].type == 228 || Main.npc[num152].type == 229 || Main.npc[num152].type == 368 || Main.npc[num152].type == 369 || Main.npc[num152].type == 550 || Main.npc[num152].type == 441 || Main.npc[num152].type == 588))
                                            {
                                                array2[num151] = num152;
                                                num151++;
                                                if (num151 >= num150)
                                                    break;
                                            }
                                        }

                                        if (num151 > 0)
                                        {
                                            int num153 = array2[Main.rand.Next(num151)];
                                            Main.npc[num153].Teleport(new Vector2(num142 - Main.npc[num153].width / 2, num143 - Main.npc[num153].height - 1), 14);
                                        }
                                    }
                                }
                                break;
                            case 41:
                                {
                                    if (!RuntimeGeneral.CheckMech(num139, num138, 300))
                                    {
                                        int num146 = 50;
                                        int[] array = new int[num146];
                                        int num147 = 0;
                                        for (int num148 = 0; num148 < Main.maxNPCs; num148++)
                                        {
                                            if (Main.npc[num148].active && (Main.npc[num148].type == 18 || Main.npc[num148].type == 20 || Main.npc[num148].type == 124 || Main.npc[num148].type == 178 || Main.npc[num148].type == 208 || Main.npc[num148].type == 353 || Main.npc[num148].type == 633 || Main.npc[num148].type == 663))
                                            {
                                                array[num147] = num148;
                                                num147++;
                                                if (num147 >= num146)
                                                    break;
                                            }
                                        }

                                        if (num147 > 0)
                                        {
                                            int num149 = array[Main.rand.Next(num147)];
                                            Main.npc[num149].Teleport(new Vector2(num142 - Main.npc[num149].width / 2, num143 - Main.npc[num149].height - 1), 14);
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    if (num144 >= 0)
                    {
                        Main.npc[num144].value = 0f;
                        Main.npc[num144].npcSlots = 0f;
                        Main.npc[num144].SpawnedFromStatue = true;
                        Main.npc[num144].CanBeReplacedByOtherNPCs = true;
                    }
                }
                break;
            case TileID.MushroomStatue:
                {
                    int num113 = tile.TileFrameY / 18;
                    num113 %= 3;
                    int num114 = j - num113;
                    int num115;
                    for (num115 = tile.TileFrameX / 18; num115 >= 2; num115 -= 2)
                    {
                    }

                    num115 = i - num115;
                    // SkipWire(num115, num114);
                    // SkipWire(num115, num114 + 1);
                    // SkipWire(num115, num114 + 2);
                    // SkipWire(num115 + 1, num114);
                    // SkipWire(num115 + 1, num114 + 1);
                    // SkipWire(num115 + 1, num114 + 2);
                    short num116 = (short)((Main.tile[num115, num114].TileFrameX != 0) ? (-216) : 216);
                    for (int num117 = 0; num117 < 2; num117++)
                    {
                        for (int num118 = 0; num118 < 3; num118++)
                        {
                            Main.tile[num115 + num117, num114 + num118].TileFrameX += num116;
                        }
                    }

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendTileSquare(-1, num115, num114, 2, 3);

                    Animation.NewTemporaryAnimation((num116 <= 0) ? 1 : 0, TileID.MushroomStatue, num115, num114);
                }
                break;
            case TileID.CatBast:
                {
                    int num75 = tile.TileFrameY / 18;
                    num75 %= 3;
                    int num76 = j - num75;
                    int num77;
                    for (num77 = tile.TileFrameX / 18; num77 >= 2; num77 -= 2)
                    {
                    }

                    num77 = i - num77;
                    if (!WorldGen.ValidateTileSquareIsActiveAndOfType(num77, num76, 2, 3, type))
                        break;

                    // SkipWire(num77, num76);
                    // SkipWire(num77, num76 + 1);
                    // SkipWire(num77, num76 + 2);
                    // SkipWire(num77 + 1, num76);
                    // SkipWire(num77 + 1, num76 + 1);
                    // SkipWire(num77 + 1, num76 + 2);
                    short num78 = (short)((Main.tile[num77, num76].TileFrameX >= 72) ? (-72) : 72);
                    for (int num79 = 0; num79 < 2; num79++)
                    {
                        for (int num80 = 0; num80 < 3; num80++)
                        {
                            Main.tile[num77 + num79, num76 + num80].TileFrameX += num78;
                        }
                    }

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendTileSquare(-1, num77, num76, 2, 3);
                }
                break;
        }
    }
}
