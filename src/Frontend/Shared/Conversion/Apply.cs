using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Conversion;

internal static class Apply
{
    public static void Execute()
    {
        Access.Instance.Status("Applying wiring...");
        foreach (var lamp in WiringGraph.Lamps)
            ApplyLogicLamp(lamp);
        foreach (var gate in WiringGraph.Gates)
            ApplyLogicGate(gate);
    }

    private static void ApplyLogicLamp(Lamp lamp)
    {
        if (lamp.Type == LampID.None) throw new Exception($"{Validate.At(lamp)} Type expect lamp");

        short frameX = lamp.Type switch
        {
            LampID.Off   => 0 * 18,
            LampID.On    => 1 * 18,
            LampID.Fault => 2 * 18,
            _ => 0,
        };

        ApplyTile(lamp.Origin.X, lamp.Origin.Y, TileID.LogicGateLamp, frameX, 0);
    }

    private static void ApplyLogicGate(Gate gate)
    {
        if (gate.Type == GateID.None) throw new Exception($"{Validate.At(gate)} Type expect gate");

        if (gate.Type == GateID.Fault)
        {
            ApplyTile(gate.Origin.X, gate.Origin.Y, TileID.LogicGate, 2 * 18, 0);
            return;
        }

        var lamps = gate.Fanin.OfType<Lamp>().ToList();
        var onCount = lamps.Count(l => l.Type == LampID.On);
        var on = gate.Type switch
        {
            GateID.AND   => onCount == lamps.Count,
            GateID.OR    => onCount > 0,
            GateID.NAND  => onCount != lamps.Count,
            GateID.NOR   => onCount == 0,
            GateID.XOR   => onCount == 1,
            GateID.XNOR  => onCount != 1,
            _ => false
        };

        short frameX = (short)(on ? 18 : 0);
        short frameY = gate.Type switch
        {
            GateID.AND   => 0 * 18,
            GateID.OR    => 1 * 18,
            GateID.NAND  => 2 * 18,
            GateID.NOR   => 3 * 18,
            GateID.XOR   => 4 * 18,
            GateID.XNOR  => 5 * 18,
            _ => 0
        };

        ApplyTile(gate.Origin.X, gate.Origin.Y, TileID.LogicGate, frameX, frameY);
    }

    private static void ApplyTile(int x, int y, ushort type, short frameX, short frameY)
    {
        var tile = Access.Instance.GetTile(x, y);
        tile.HasTile = true;
        tile.type = type;
        tile.frameX = frameX;
        tile.frameY = frameY;
        Access.Instance.SetTile(x, y, tile);
    }
}
