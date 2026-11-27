using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows named pipe regression");
if (args.Length != 1) throw new ArgumentException("Pass the path to macha.exe");
string exe = Path.GetFullPath(args[0]);
if (!File.Exists(exe)) throw new FileNotFoundException(exe);
byte[] Header(ushort tag=1, long id=1, int length=0, uint magic=0xBADBEEF)
{
    byte[] b=new byte[20];
    BinaryPrimitives.WriteUInt32LittleEndian(b,magic);
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(4),1);
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6),tag);
    BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(8),id);
    BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16),length);
    return b;
}
async Task Request(NamedPipeServerStream pipe, ushort tag, long id, CancellationToken ct)
{
    await pipe.WriteAsync(Header(tag,id),ct);
    byte[] hdr=new byte[20]; await pipe.ReadExactlyAsync(hdr,ct);
    if (BinaryPrimitives.ReadUInt32LittleEndian(hdr)!=0xBADBEEF ||
        BinaryPrimitives.ReadUInt16LittleEndian(hdr.AsSpan(4))!=1 ||
        BinaryPrimitives.ReadUInt16LittleEndian(hdr.AsSpan(6))!=tag+1 ||
        BinaryPrimitives.ReadInt64LittleEndian(hdr.AsSpan(8))!=id ||
        BinaryPrimitives.ReadInt32LittleEndian(hdr.AsSpan(16))!=5) throw new Exception("Bad empty-mode ACK header");
    byte[] body=new byte[5]; await pipe.ReadExactlyAsync(body,ct);
    if (body.Any(x=>x!=0)) throw new Exception("Bad empty-mode ACK body");
}
async Task Check(string name, Func<NamedPipeServerStream,CancellationToken,Task> act, int exit, string marker)
{
    string pipeName="WireWarp-RC1-"+Guid.NewGuid().ToString("N");
    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
    var psi=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true,CreateNoWindow=true};
    psi.ArgumentList.Add("serve"); psi.ArgumentList.Add("--empty"); psi.ArgumentList.Add("--pipe");
    psi.ArgumentList.Add(@"\\.\pipe\"+pipeName);
    using var proc=Process.Start(psi) ?? throw new Exception("Could not launch backend");
    using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var stderr=proc.StandardError.ReadToEndAsync(); var stdout=proc.StandardOutput.ReadToEndAsync();
    try {
        await pipe.WaitForConnectionAsync(cts.Token);
        try { await act(pipe,cts.Token); } catch (IOException) when (exit != 0) { /* expected peer may close while reading */ }
        pipe.Dispose();
        await proc.WaitForExitAsync(cts.Token);
        string log=await stderr+await stdout;
        if (proc.ExitCode!=exit || !log.Contains(marker,StringComparison.Ordinal))
            throw new Exception($"{name}: exit={proc.ExitCode}; {log}");
        Console.WriteLine("PASS: "+name);
    } finally { if (!proc.HasExited) { proc.Kill(); await proc.WaitForExitAsync(); } }
}
await Check("clean EOF", (p,ct)=>Task.CompletedTask,0,"frontend closed the connection");
await Check("startup + shutdown + close", async (p,ct)=>{await Request(p,1,1,ct);await Request(p,11,2,ct);},0,"frontend closed the connection");
await Check("truncated header", async (p,ct)=>{await p.WriteAsync(Header().AsMemory(0,10),ct); await Task.Run(p.WaitForPipeDrain).WaitAsync(ct);},1,"Truncated");
await Check("truncated body", async (p,ct)=>{await p.WriteAsync(Header(length:4),ct);await p.WriteAsync(new byte[2],ct); await Task.Run(p.WaitForPipeDrain).WaitAsync(ct);},1,"Truncated");
await Check("bad magic", async (p,ct)=>{await p.WriteAsync(Header(magic:1),ct); await p.ReadAsync(new byte[1],ct);},1,"BadHeader");
await Check("invalid request tag", async (p,ct)=>{await p.WriteAsync(Header(tag:65535),ct); await p.ReadAsync(new byte[1],ct);},1,"BadTag");
await Check("ID gap", async (p,ct)=>{await Request(p,1,1,ct);await p.WriteAsync(Header(id:3),ct);await p.ReadAsync(new byte[1],ct);},1,"IdGap");
Console.WriteLine("PASS: 7 actual backend named-pipe cases (empty mode, no game simulation)");
