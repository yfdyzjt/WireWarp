namespace WireWarp.Frontend.Shared.Terraria;

public struct Tile
{
    public ushort TileType;
    public short TileFrameX;
    public short TileFrameY;

    public bool HasTile;
    public bool HasActuator;
    public bool IsActuated;

    public bool RedWire;
    public bool BlueWire;
    public bool GreenWire;
    public bool YellowWire;
}
