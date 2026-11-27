using Terraria;
using Terraria.ID;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void ConveyorBelts(int portId, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (!tile.HasActuator)
        {
            if (tile.TileType == TileID.ConveyorBeltLeft)
                tile.TileType = TileID.ConveyorBeltRight;
            else
                tile.TileType = TileID.ConveyorBeltLeft;

            WorldGen.SquareTileFrame(i, j);
            NetMessage.SendTileSquare(-1, i, j);
        }
    }
}
