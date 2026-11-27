using Terraria;
using Terraria.ID;
using WireWarp.Frontend.Shared;

namespace WireWarp.Frontend.tModLoader.IO;

internal static class RuntimeGeneral
{
    public static void Tick() => UpdateMech();
    public static void Reset() => ClearAll();

    private const int MaxMech = 1000;

    private static readonly int[] _mechX = new int[MaxMech];
    private static readonly int[] _mechY = new int[MaxMech];
    private static readonly int[] _mechTime = new int[MaxMech];
    private static int _numMechs;

    public static int cannonCoolDown = 0;
    public static int bunnyCannonCoolDown = 0;
    public static int snowballCannonCoolDown = 0;

    private static void ClearAll()
    {
        for (int j = 0; j < MaxMech; j++)
        {
            _mechTime[j] = 0;
            _mechX[j] = 0;
            _mechY[j] = 0;
        }

        _numMechs = 0;
    }

    public static bool CheckMech(int i, int j, int time)
    {
        for (int k = 0; k < _numMechs; k++)
        {
            if (_mechX[k] == i && _mechY[k] == j)
                return false;
        }

        if (_numMechs < MaxMech)
        {
            _mechX[_numMechs] = i;
            _mechY[_numMechs] = j;
            _mechTime[_numMechs] = time;
            _numMechs++;
            return true;
        }

        return false;
    }

    private static void UpdateMech()
    {
        if (cannonCoolDown > 0)
            cannonCoolDown--;

        if (bunnyCannonCoolDown > 0)
            bunnyCannonCoolDown--;

        if (snowballCannonCoolDown > 0)
            snowballCannonCoolDown--;

        Wiring.SetCurrentUser();
        for (int num = _numMechs - 1; num >= 0; num--)
        {
            _mechTime[num]--;
            int num2 = _mechX[num];
            int num3 = _mechY[num];
            if (!WorldGen.InWorld(num2, num3, 1))
            {
                _numMechs--;
            }
            else
            {
                Tile tile = Main.tile[num2, num3];
                /*if (tile == null)
                {
                    _numMechs--;
                }
                else*/
                {
                    if (tile.HasTile && tile.TileType == /*144*/TileID.Timers)
                    {
                        if (tile.TileFrameY == 0)
                        {
                            _mechTime[num] = 0;
                        }
                        else
                        {
                            int num4 = tile.TileFrameX / 18;
                            switch (num4)
                            {
                                case 0:
                                    num4 = 60;
                                    break;
                                case 1:
                                    num4 = 180;
                                    break;
                                case 2:
                                    num4 = 300;
                                    break;
                                case 3:
                                    num4 = 30;
                                    break;
                                case 4:
                                    num4 = 15;
                                    break;
                            }

                            if (Math.IEEERemainder(_mechTime[num], num4) == 0.0)
                            {
                                _mechTime[num] = 18000;
                                Runtime.HitInput(_mechX[num], _mechY[num], false);
                            }
                        }
                    }

                    if (_mechTime[num] <= 0)
                    {
                        if (tile.HasTile && tile.TileType == /*144*/TileID.Timers)
                        {
                            tile.TileFrameY = 0;
                            NetMessage.SendTileSquare(-1, _mechX[num], _mechY[num]);
                        }

                        if (tile.HasTile && tile.TileType == /*411*/TileID.Detonator)
                        {
                            int num5 = tile.TileFrameX % 36 / 18;
                            int num6 = tile.TileFrameY % 36 / 18;
                            int num7 = _mechX[num] - num5;
                            int num8 = _mechY[num] - num6;
                            int num9 = 36;
                            if (Main.tile[num7, num8].TileFrameX >= 36)
                                num9 = -36;

                            for (int i = num7; i < num7 + 2; i++)
                            {
                                for (int j = num8; j < num8 + 2; j++)
                                {
                                    if (WorldGen.InWorld(i, j, 1))
                                    {
                                        Tile tile2 = Main.tile[i, j];
                                        /*if (tile2 != null)*/
                                            tile2.TileFrameX = (short)(tile2.TileFrameX + num9);
                                    }
                                }
                            }

                            NetMessage.SendTileSquare(-1, num7, num8, 2, 2);
                        }

                        for (int k = num; k < _numMechs; k++)
                        {
                            _mechX[k] = _mechX[k + 1];
                            _mechY[k] = _mechY[k + 1];
                            _mechTime[k] = _mechTime[k + 1];
                        }

                        _numMechs--;
                    }
                }
            }
        }
    }
}
