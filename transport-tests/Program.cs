using System.IO.Pipes;
using WireWarp.Frontend.Shared;

// Terraria and its backend must be closed: this test uses the production pipe name.
async Task Check(bool supports)
{
    var cases = new[] { ("reference","reference",2,false), ("compiled","compiled",100_000,false),
        ("fast","fast",100_000,true), ("fast","reference",1,true), ("reference","fast",15,false),
        ("fast","compiled",0,true), ("fast","fast",3,false), ("indexed","fast",0,false), ("async","async",100_000,false) };
    var server = Task.Run(() =>
    {
        using var peer = new NamedPipeClientStream(".","WireWarp",PipeDirection.InOut);
        peer.Connect(10000);
        using var r = new BinaryReader(peer, System.Text.Encoding.UTF8,true);
        using var w = new BinaryWriter(peer, System.Text.Encoding.UTF8,true);
        long Request(ushort tag)
        {
            if (r.ReadUInt32()!=0xBADBEEF || r.ReadUInt16()!=1 || r.ReadUInt16()!=tag) throw new Exception("Request header");
            long id=r.ReadInt64(); int length=r.ReadInt32(); if (r.ReadBytes(length).Length!=length) throw new Exception("Request body");
            return id;
        }
        void Reply(ushort tag, long id, byte[] bytes)
        {
            w.Write(0xBADBEEFu); w.Write((ushort)1); w.Write(tag); w.Write(id); w.Write(bytes.Length); w.Write(bytes); w.Flush();
        }
        long startup=Request(1);
        using(var ms=new MemoryStream()) { using var body=new BinaryWriter(ms); body.Write(0); body.Write(""); if(supports) body.Write("WW38:compact16"u8); Reply(2,startup,ms.ToArray()); }
        long sync=Request(3);
        using(var ms=new MemoryStream()) { using var body=new BinaryWriter(ms); body.Write(0); body.Write(""); if(supports) body.Write("WW40:v4.0"u8); Reply(4,sync,ms.ToArray()); }
        foreach (var c in cases)
        {
            ushort tag=(ushort)(supports && c.Item1=="fast" ? 13 : 9);
            long id=Request(tag);
            bool compact=tag==13 && c.Item4;
            using var ms=new MemoryStream(); using var body=new BinaryWriter(ms);
            body.Write(0); body.Write("ack ✓"); body.Write(compact ? ~c.Item3 : c.Item3);
            for(int i=0;i<c.Item3;i++)
                if(compact) body.Write((ushort)(i%17)); else { body.Write(i%17); body.Write(2); }
            Reply((ushort)(tag+1),id,ms.ToArray());
        }
    });
    Transport.Open();
    try
    {
        if(Transport.SendStartup().status!=0 || Transport.CompactSupported!=supports) throw new Exception("Capability mismatch");
        if(Transport.SendSyncTo(new byte[32], "test").status!=0 || Transport.BackendMode!=(supports?"v4.0":"unknown (not reported)")) throw new Exception("Backend mode report mismatch");
        long expectedBytes=0, compactFrames=0;
        foreach(var c in cases)
        {
            Runtime.Mode=c.Item1;
            Transport.SendFrameAsync(true,1,[(2,1)]);
            Runtime.Mode=c.Item2;
            var (ack,outputs)=Transport.CompleteFrame();
            bool compact=supports && c.Item1=="fast" && c.Item4;
            if(ack.status!=0 || ack.message!="ack ✓" || outputs.Count!=c.Item3) throw new Exception("Response mismatch");
            for(int i=0;i<outputs.Count;i++) if(outputs[i]!=(i%17,compact?1:2)) throw new Exception("Record mismatch");
            expectedBytes+=4+c.Item3*(compact?2:8);
            if(compact) compactFrames++;
        }
        if(Transport.OutputPayloadBytes!=expectedBytes || Transport.CompactFrames!=compactFrames || Transport.RleFrames!=cases.Length-compactFrames) throw new Exception("IPC counters");
        await server;
    }
    finally { Transport.Close(); }
}
await Check(false);
await Check(true);
Console.WriteLine("PASS: 18 named-pipe frame responses, legacy and compact negotiation, mode transitions, growing and shrinking buffers, RLE fallback and IPC counters.");
