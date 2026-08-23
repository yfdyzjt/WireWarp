using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.File;

public static partial class IOFile
{
    private const int GroupCount = 8;

    public static void Serialize(BinaryWriter w)
    {
        WriteGroups(w);
    }

    public static void Deserialize(BinaryReader r)
    {
        if (r.ReadInt32() != GroupCount) throw new InvalidDataException("IO serializer group count mismatch");

        var starts = new int[GroupCount];
        for (var i = 0; i < GroupCount; i++)
            starts[i] = r.ReadInt32();

        if (ReadInputs(r) != starts[1]) throw new InvalidDataException("IO serializer group 0 length mismatch");
        if (ReadOutputs(r) != starts[2]) throw new InvalidDataException("IO serializer group 1 length mismatch");
        if (ReadLampPos(r) != starts[3]) throw new InvalidDataException("IO serializer group 2 length mismatch");
        if (ReadGatePos(r) != starts[4]) throw new InvalidDataException("IO serializer group 3 length mismatch");
        if (ReadTeleporter(r) != starts[5]) throw new InvalidDataException("IO serializer group 4 length mismatch");
        if (ReadPumps(r) != starts[6]) throw new InvalidDataException("IO serializer group 5 length mismatch");
        if (ReadWireBulb(r) != starts[7]) throw new InvalidDataException("IO serializer group 6 length mismatch");
    }

    private static long ReadInputs(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var x = r.ReadInt32();
            var y = r.ReadInt32();
            var portId = r.ReadInt32();
            var type = (InputID)r.ReadByte();

            IOGraph.SetInput((x, y), portId, type);
        }

        return r.BaseStream.Position;
    }

    private static long ReadOutputs(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var portId = r.ReadInt32();
            var x = r.ReadInt32();
            var y = r.ReadInt32();
            var type = (OutputID)r.ReadByte();

            IOGraph.SetOutput(portId, (x, y), type);
        }

        return r.BaseStream.Position;
    }

    private static long ReadLampPos(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var id = r.ReadInt32();
            var x = r.ReadInt32();
            var y = r.ReadInt32();

            IOGraph.SetLampPos(id, (x, y));
        }

        return r.BaseStream.Position;
    }

    private static long ReadGatePos(BinaryReader r)
    {
        var count = r.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var id = r.ReadInt32();
            var x = r.ReadInt32();
            var y = r.ReadInt32();

            IOGraph.SetGatePos(id, (x, y));
        }

        return r.BaseStream.Position;
    }

    private static void WriteGroups(BinaryWriter w)
    {
        w.Write(GroupCount);

        var groupStartPos = w.BaseStream.Position;
        for (var i = 0; i < GroupCount; i++)
            w.Write(0);

        var starts = new long[GroupCount];

        starts[0] = WriteInputs(w);
        starts[1] = WriteOutputs(w);
        starts[2] = WriteLampPos(w);
        starts[3] = WriteGatePos(w);
        starts[4] = WriteTeleporter(w);
        starts[5] = WritePumps(w);
        starts[6] = WriteWireBulb(w);
        starts[7] = w.BaseStream.Position;

        w.BaseStream.Position = groupStartPos;
        for (var i = 0; i < GroupCount; i++)
            w.Write((uint)starts[i]);
    }

    private static long WriteInputs(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var inputs = IOGraph.Inputs.OrderBy(kv => kv.Value.portId).ToList();

        w.Write(inputs.Count);
        foreach (var (pos, (portId, type)) in inputs)
        {
            w.Write(pos.x);
            w.Write(pos.y);
            w.Write(portId);
            w.Write((byte)type);
        }

        return start;
    }

    private static long WriteOutputs(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var outputs = IOGraph.Outputs.OrderBy(kv => kv.Key).ToList();

        w.Write(outputs.Count);
        foreach (var (portId, (pos, type)) in outputs)
        {
            w.Write(portId);
            w.Write(pos.x);
            w.Write(pos.y);
            w.Write((byte)type);
        }

        return start;
    }

    private static long WriteLampPos(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var lamps = IOGraph.LampPos.OrderBy(kv => kv.Key).ToList();

        w.Write(lamps.Count);
        foreach (var (id, (x, y)) in lamps)
        {
            w.Write(id);
            w.Write(x);
            w.Write(y);
        }

        return start;
    }

    private static long WriteGatePos(BinaryWriter w)
    {
        var start = w.BaseStream.Position;

        var gates = IOGraph.GatePos.OrderBy(kv => kv.Key).ToList();

        w.Write(gates.Count);
        foreach (var (id, (x, y)) in gates)
        {
            w.Write(id);
            w.Write(x);
            w.Write(y);
        }

        return start;
    }
}
