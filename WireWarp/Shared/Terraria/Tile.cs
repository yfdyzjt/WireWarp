namespace WireWarp.Frontend.Shared.Terraria;

public struct Tile
{
    public ushort type;
    public short frameX;
    public short frameY;

    public bool HasTile;
    public bool HasActuator;
    public bool IsActuated;

    public bool RedWire;
    public bool BlueWire;
    public bool GreenWire;
    public bool YellowWire;
}
