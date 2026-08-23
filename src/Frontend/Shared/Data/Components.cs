namespace WireWarp.Frontend.Shared.Data;

public interface IConnectable
{
    int Id { get; }

    byte Type { get; }
    
    HashSet<IConnectable> Fanin { get; }
    HashSet<IConnectable> Fanout { get; }
}

public class Wire : IConnectable
{
    public int Id { get; set; }
    
    public WireID Type { get; init; }
    byte IConnectable.Type => (byte)Type;

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];

    public HashSet<(int X, int Y)> Sources { get; } = [];
    public HashSet<(int X, int Y)> Drains { get; } = [];
}

public class Lamp : IConnectable
{
    public int Id { get; set; }
    
    public LampID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    
    public (int X, int Y) Origin { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}

public class Gate : IConnectable
{
    public int Id { get; set; }
    
    public GateID Type { get; init; }
    byte IConnectable.Type => (byte)Type;

    public (int X, int Y) Origin { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}

public class Input : IConnectable
{
    public int Id { get; set; }
    
    public InputID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    
    public (int X, int Y) Origin { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}

public class Output : IConnectable
{
    public int Id { get; set; }
    
    public OutputID Type { get; init; }
    byte IConnectable.Type => (byte)Type;
    
    public (int X, int Y) Origin { get; set; }

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}

public class InputPort : IConnectable
{
    public int Id { get; set; }
    public int PortId => Id - WiringGraph.InputPortOffset;

    InputID Type => Fanin.OfType<Input>().FirstOrDefault() 
        is Input i ? i.Type : InputID.None;
    byte IConnectable.Type => (byte)Type;

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}

public class OutputPort : IConnectable
{
    public int Id { get; set; }
    public int PortId => Id - WiringGraph.OutputPortOffset;

    OutputID Type => Fanout.OfType<Output>().FirstOrDefault()
        is Output o ? o.Type : OutputID.None;
    byte IConnectable.Type => (byte)Type;

    public HashSet<IConnectable> Fanin { get; } = [];
    public HashSet<IConnectable> Fanout { get; } = [];
}
