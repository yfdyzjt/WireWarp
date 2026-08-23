namespace WireWarp.Frontend.Shared.Data;

public enum WireID : byte
{
    None = 0,
    Red,
    Blue,
    Green,
    Yellow,
}

public enum JunctionBoxID : byte
{
    None = 0,
    UpDown,
    UpLeft,
    UpRight,
}


public enum LampID : byte
{
    None = 0,
    On,
    Off,
    Fault,
}

public enum GateID : byte
{
    None = 0,
    AND,
    NAND,
    OR,
    NOR,
    XOR,
    XNOR,
    Fault,
}

public enum InputID : byte
{
    None = 0,
    PressurePlates,
    PressurePlateTrack,
    LogicSensor,
    WeightedPressurePlate,
    ProjectilePressurePad,
    GolfHole,
    GemLocks,
    Switches,
    GeyserTrap,
    Timers,
    FakeContainers,
    DeadMansChest,
    Lever,
    Detonator,
}

public enum OutputID : byte
{
    None = 0,
    Actuator,
    Timers,
    ConveyorBelts,
    Gemsparks,
    Chimney,
    SillyBalloonMachine,
    Detonator,
    SunAndMoondial,
    AnnouncementBox,
    Fireplace,
    CannonsLeft,
    CannonsRight,
    CannonsShot,
    PortalGunStationShot,
    PortalGunStationChange,
    SnowballLauncherLeft,
    SnowballLauncherRight,
    SnowballLauncherShot,
    Campfires,
    ActiveStoneBlocks,
    TrapdoorOpen,
    TrapdoorClosed,
    TallGates,
    OpenDoors,
    ClosedDoors,
    Fireworks,
    Toilets,
    FireworksBox,
    FireworkFountain,
    Teleporter,
    Torches,
    WireBulb,
    HolidayLights,
    BubbleMachine,
    FogMachine,
    HangingLanterns,
    Lamps,
    Lights,
    VolcanoSmall,
    VolcanoLarge,
    Chandeliers,
    MinecartTrack,
    Candles,
    Lampposts,
    Traps,
    GeyserTrap,
    MusicBoxes,
    WaterFountain,
    Monoliths,
    PartyMonolith,
    Explosives,
    LandMine,
    Pumps,
    Statues,
    Grates,
    PixelBox,
}
