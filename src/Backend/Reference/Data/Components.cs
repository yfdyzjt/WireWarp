namespace WireWarp.Backend.Reference.Data;

public interface IConnectable
{
    int Id { get; }
    byte Type { get; }
    List<IConnectable> Fanin { get; }
    List<IConnectable> Fanout { get; }
}

public class InputPort : IConnectable
{
    public int Id { get; init; }
    public InputID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    public List<IConnectable> Fanin { get; } = [];
    public List<IConnectable> Fanout { get; } = [];
}

public class OutputPort : IConnectable
{
    public int Id { get; init; }
    public OutputID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    public List<IConnectable> Fanin { get; } = [];
    public List<IConnectable> Fanout { get; } = [];
}

public class Lamp : IConnectable
{
    public int Id { get; init; }
    public LampID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    public List<IConnectable> Fanin { get; } = [];
    public List<IConnectable> Fanout { get; } = [];
}

public class Gate : IConnectable
{
    public int Id { get; init; }
    public GateID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    public List<IConnectable> Fanin { get; } = [];
    public List<IConnectable> Fanout { get; } = [];
}

public class Wire : IConnectable
{
    public int Id { get; init; }
    public WireID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    public List<IConnectable> Fanin { get; } = [];
    public List<IConnectable> Fanout { get; } = [];
}
