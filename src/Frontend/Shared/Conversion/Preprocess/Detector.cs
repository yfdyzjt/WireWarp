using WireWarp.Frontend.Shared.ID;
using WireWarp.Frontend.Shared.Terraria;
using WireWarp.Frontend.Shared.Terraria.ID;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Detector
{
    public static bool HasWire(Tile tile) =>
        tile.RedWire || tile.BlueWire || tile.GreenWire || tile.YellowWire;

    public static bool HasWire(Tile tile, WireID color) => color switch
    {
        WireID.Red => tile.RedWire,
        WireID.Blue => tile.BlueWire,
        WireID.Green => tile.GreenWire,
        WireID.Yellow => tile.YellowWire,
        _ => false,
    };

    public static GateID DetectGate(Tile tile)
    {
        if (!tile.HasTile || tile.type != TileID.LogicGate)
            return GateID.None;

        return tile.frameX switch
        {
            0 * 18 or 1 * 18 => tile.frameY switch
            {
                0 * 18 => GateID.AND,
                1 * 18 => GateID.OR,
                2 * 18 => GateID.NAND,
                3 * 18 => GateID.NOR,
                4 * 18 => GateID.XOR,
                5 * 18 => GateID.XNOR,
                _ => GateID.None,
            },
            2 * 18 => GateID.Fault,
            _ => GateID.None,
        };
    }

    public static LampID DetectLamp(Tile tile)
    {
        if (!tile.HasTile || tile.HasActuator || tile.type != TileID.LogicGateLamp)
            return LampID.None;

        return tile.frameX switch
        {
            0 * 18 => LampID.Off,
            1 * 18 => LampID.On,
            2 * 18 => LampID.Fault,
            _ => LampID.None,
        };
    }

    public static JunctionBoxID DetectJunctionBox(Tile tile)
    {
        if (!tile.HasTile)
            return JunctionBoxID.None;

        return tile.type switch
        {
            TileID.PixelBox => JunctionBoxID.UpDown,
            TileID.WirePipe => tile.frameX switch
            {
                0 * 18 => JunctionBoxID.UpDown,
                1 * 18 => JunctionBoxID.UpLeft,
                2 * 18 => JunctionBoxID.UpRight,
                _ => JunctionBoxID.None,
            },
            _ => JunctionBoxID.None,
        };
    }

    public static InputID DetectInput(Tile tile)
    {
        if (!tile.HasTile)
            return InputID.None;

        return tile.type switch
        {
            TileID.PressurePlates => InputID.PressurePlates,
            TileID.MinecartTrack when 20 <= tile.frameX && tile.frameX < 24 => InputID.PressurePlateTrack,
            TileID.LogicSensor => InputID.LogicSensor,
            TileID.WeightedPressurePlate => InputID.WeightedPressurePlate,
            TileID.ProjectilePressurePad => InputID.ProjectilePressurePad,
            TileID.GolfHole => InputID.GolfHole,
            TileID.GemLocks => InputID.GemLocks,
            TileID.Switches => InputID.Switches,
            TileID.GeyserTrap => InputID.GeyserTrap,
            TileID.Timers => InputID.Timers,
            TileID.FakeContainers or TileID.FakeContainers2 => InputID.FakeContainers,
            TileID.Containers2 when tile.frameX / 36 is 4 => InputID.DeadMansChest,
            TileID.Lever => InputID.Lever,
            TileID.Detonator => InputID.Detonator,
            _ => InputID.None,
        };
    }

    public static OutputID DetectOutput(Tile tile)
    {
        if (!tile.HasTile)
            return OutputID.None;

        return tile.type switch
        {
            _ when tile.HasActuator => OutputID.Actuator,
            TileID.Timers => OutputID.Timers,
            TileID.ConveyorBeltLeft or TileID.ConveyorBeltRight => OutputID.ConveyorBelts,
            _ when TileID.AmethystGemsparkOff <= tile.type && tile.type <= TileID.AmberGemspark => OutputID.Gemsparks,
            TileID.Chimney => OutputID.Chimney,
            TileID.SillyBalloonMachine => OutputID.SillyBalloonMachine,
            TileID.Detonator => OutputID.Detonator,
            TileID.Sundial or TileID.Moondial => OutputID.SunAndMoondial,
            TileID.AnnouncementBox => OutputID.AnnouncementBox,
            TileID.Fireplace => OutputID.Fireplace,
            TileID.Cannon => (tile.frameX % 72) switch
            {
                0 => OutputID.CannonsLeft,
                54 => OutputID.CannonsRight,
                18 or 36 => tile.frameX < 216
                    ? OutputID.CannonsShot
                    : (tile.frameY % 54) switch
                    {
                        0 or 18 => OutputID.PortalGunStationChange,
                        36 => OutputID.PortalGunStationShot,
                        _ => OutputID.None,
                    },
                _ => OutputID.None,
            },
            TileID.SnowballLauncher => (tile.frameX % 54) switch
            {
                0 => OutputID.SnowballLauncherLeft,
                36 => OutputID.SnowballLauncherRight,
                18 => OutputID.SnowballLauncherShot,
                _ => OutputID.None,
            },
            TileID.Campfire => OutputID.Campfires,
            TileID.ActiveStoneBlock or TileID.InactiveStoneBlock => OutputID.ActiveStoneBlocks,
            TileID.TrapdoorOpen => OutputID.TrapdoorOpen,
            TileID.TrapdoorClosed => OutputID.TrapdoorClosed,
            TileID.TallGateOpen or TileID.TallGateClosed => OutputID.TallGates,
            TileID.OpenDoor => OutputID.OpenDoors,
            TileID.ClosedDoor => OutputID.ClosedDoors,
            TileID.Firework => OutputID.Fireworks,
            TileID.Toilets => OutputID.Toilets,
            TileID.Chairs when tile.frameY / 40 is 1 or 20 => OutputID.Toilets,
            TileID.FireworksBox => OutputID.FireworksBox,
            TileID.FireworkFountain => OutputID.FireworkFountain,
            TileID.Teleporter => OutputID.Teleporter,
            TileID.Torches => OutputID.Torches,
            TileID.WireBulb => OutputID.WireBulb,
            TileID.HolidayLights => OutputID.HolidayLights,
            TileID.BubbleMachine => OutputID.BubbleMachine,
            TileID.FogMachine => OutputID.FogMachine,
            TileID.HangingLanterns => OutputID.HangingLanterns,
            TileID.Lamps => OutputID.Lamps,
            TileID.DiscoBall or TileID.ChineseLanterns or TileID.Candelabras or
            TileID.PlatinumCandelabra or TileID.PlasmaLamp => OutputID.Lights,
            TileID.VolcanoSmall => OutputID.VolcanoSmall,
            TileID.VolcanoLarge => OutputID.VolcanoLarge,
            TileID.Chandeliers => OutputID.Chandeliers,
            TileID.MinecartTrack when (30 <= tile.frameX && tile.frameX < 36) ||
                ((tile.frameX < 20 || (23 < tile.frameX && tile.frameX < 30)) &&
                 tile.frameY != -1) => OutputID.MinecartTrack,
            TileID.Candles or TileID.PlatinumCandle or TileID.WaterCandle or
            TileID.PeaceCandle or TileID.ShadowCandle => OutputID.Candles,
            TileID.Lampposts => OutputID.Lampposts,
            TileID.Traps => OutputID.Traps,
            TileID.GeyserTrap => OutputID.GeyserTrap,
            TileID.MusicBoxes or TileID.Jackolanterns => OutputID.MusicBoxes,
            TileID.WaterFountain => OutputID.WaterFountain,
            TileID.LunarMonolith or TileID.BloodMoonMonolith or
            TileID.VoidMonolith or TileID.EchoMonolith or
            TileID.ShimmerMonolith or TileID.CRTMonolith or
            TileID.RetroMonolith or TileID.NoirMonolith or
            TileID.RadioThingMonolith => OutputID.Monoliths,
            TileID.PartyMonolith => OutputID.PartyMonolith,
            TileID.Explosives => OutputID.Explosives,
            TileID.LandMine => OutputID.LandMine,
            TileID.InletPump or TileID.OutletPump => OutputID.Pumps,
            TileID.BoulderStatue or TileID.MushroomStatue or
            TileID.CatBast => OutputID.Statues,
            TileID.Statues when !(tile.frameX / 36 is 0 or 1 or 3 or 6 or 11 or 12 or 14 or 15 or 19 or
                20 or 21 or 22 or 24 or 25 or 26 or 29 or 31 or 32 or 33 or 36 or 38 or 39 or 43 or 44 or 45)
                => OutputID.Statues,
            TileID.Grate or TileID.GrateClosed => OutputID.Grates,
            TileID.PixelBox => OutputID.PixelBox,
            _ => OutputID.None,
        };
    }

    public static (int x, int y) GetInputSize(InputID type) => type switch
    {
        InputID.GemLocks => (3, 3),
        InputID.Lever or InputID.Detonator or InputID.DeadMansChest or InputID.FakeContainers => (2, 2),
        InputID.GeyserTrap => (2, 1),
        _ => (1, 1),
    };

    public static (int x, int y) GetOutputSize(OutputID type) => type switch
    {
        OutputID.Lampposts => (1, 6),
        OutputID.TallGates => (1, 5),
        OutputID.SillyBalloonMachine or OutputID.Chimney or
        OutputID.Chandeliers or OutputID.PartyMonolith => (3, 3),
        OutputID.Fireplace or OutputID.Campfires or OutputID.BubbleMachine => (3, 2),
        OutputID.CannonsShot or OutputID.SunAndMoondial or OutputID.OpenDoors or
        OutputID.WaterFountain or OutputID.Monoliths or OutputID.Statues => (2, 3),
        OutputID.Teleporter => (3, 1),
        OutputID.SnowballLauncherRight or OutputID.SnowballLauncherLeft or
        OutputID.SnowballLauncherShot or OutputID.CannonsRight or OutputID.CannonsLeft or
        OutputID.ClosedDoors or OutputID.Lamps => (1, 3),
        OutputID.PortalGunStationChange or OutputID.Detonator or
        OutputID.AnnouncementBox or OutputID.TrapdoorOpen or OutputID.FireworksBox or
        OutputID.FogMachine or OutputID.Lights or OutputID.VolcanoLarge or
        OutputID.MusicBoxes or OutputID.Pumps => (2, 2),
        OutputID.PortalGunStationShot or OutputID.TrapdoorClosed or OutputID.GeyserTrap => (2, 1),
        OutputID.Fireworks or OutputID.Toilets or OutputID.FireworkFountain or
        OutputID.HangingLanterns => (1, 2),
        _ => (1, 1),
    };

    public static (int x, int y) GetInputOrigin(InputID type, int x, int y, short frameX, short frameY)
    {
        var (sx, sy) = GetInputSize(type);
        return (x - frameX % (sx * 18) / 18, y - frameY % (sy * 18) / 18);
    }

    public static (int x, int y) GetOutputOrigin(OutputID type, int x, int y, short frameX, short frameY)
    {
        var (sx, sy) = GetOutputSize(type);
        return (x - frameX % (sx * 18) / 18, y - frameY % (sy * 18) / 18);
    }
}
