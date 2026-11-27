using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void Teleporter(int portId, int i, int j)
    {
        if (!IOExtra.Teleporter.TryGetValue(portId, out var pair)) return;

        Tile tile = Main.tile[pair.source.x, pair.source.y];
        Vector2 sourcePos = new Vector2(pair.source.x - tile.TileFrameX / 18, pair.source.y);
        if (tile.IsHalfBlock)
            sourcePos.Y += 0.5f;

        Tile tile2 = Main.tile[pair.target.x, pair.target.y];
        Vector2 targetPos = new Vector2(pair.target.x - tile2.TileFrameX / 18, pair.target.y);
        if (tile2.IsHalfBlock)
            targetPos.Y += 0.5f;

        Teleport(sourcePos, targetPos);
    }

    private static void Teleport(Vector2 source, Vector2 target)
    {
        if (source.X < target.X + 3f && source.X > target.X - 3f && source.Y > target.Y - 3f && source.Y < target.Y)
            return;

        Rectangle[] array = new Rectangle[2];
        array[0].X = (int)(source.X * 16f);
        array[0].Width = 48;
        array[0].Height = 48;
        array[0].Y = (int)(source.Y * 16f - (float)array[0].Height);
        array[1].X = (int)(target.X * 16f);
        array[1].Width = 48;
        array[1].Height = 48;
        array[1].Y = (int)(target.Y * 16f - (float)array[1].Height);

        for (int i = 0; i < 2; i++)
        {
            Vector2 vector = new Vector2(array[1].X - array[0].X, array[1].Y - array[0].Y);
            if (i == 1)
                vector = new Vector2(array[0].X - array[1].X, array[0].Y - array[1].Y);

            if (!Wiring.blockPlayerTeleportationForOneIteration)
            {
                for (int j = 0; j < 255; j++)
                {
                    if (Main.player[j].active && !Main.player[j].dead && !Main.player[j].teleporting && TeleporterHitboxIntersects(array[i], Main.player[j].Hitbox))
                    {
                        Vector2 vector2 = Main.player[j].position + vector;
                        Main.player[j].teleporting = true;
                        if (Main.netMode == /*2*/NetmodeID.Server)
                            RemoteClient.CheckSection(j, vector2);

                        Main.player[j].Teleport(vector2);
                        if (Main.netMode == /*2*/NetmodeID.Server)
                            NetMessage.SendData(/*65*/MessageID.TeleportEntity, -1, -1, /*null,*/ number: 0, number2: j, number3: vector2.X, number4: vector2.Y);
                    }
                }
            }

            for (int k = 0; k < Main.maxNPCs; k++)
            {
                if (Main.npc[k].active && !Main.npc[k].teleporting && Main.npc[k].lifeMax > 5 && !Main.npc[k].boss && !Main.npc[k].noTileCollide)
                {
                    int type = Main.npc[k].type;
                    if (!NPCID.Sets.TeleportationImmune[type] && TeleporterHitboxIntersects(array[i], Main.npc[k].Hitbox))
                    {
                        Main.npc[k].teleporting = true;
                        Main.npc[k].Teleport(Main.npc[k].position + vector);
                    }
                }
            }
        }

        for (int l = 0; l < 255; l++)
        {
            Main.player[l].teleporting = false;
        }

        for (int m = 0; m < Main.maxNPCs; m++)
        {
            Main.npc[m].teleporting = false;
        }
    }

    private static bool TeleporterHitboxIntersects(Rectangle teleporter, Rectangle entity)
    {
        Rectangle rectangle = Rectangle.Union(teleporter, entity);
        if (rectangle.Width <= teleporter.Width + entity.Width)
            return rectangle.Height <= teleporter.Height + entity.Height;

        return false;
    }
}
