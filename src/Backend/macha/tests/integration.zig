const std = @import("std");
const macha = @import("macha");
const protocol = macha.protocol;
const util = macha.util;

const testing = std.testing;
const hash: [util.HASH_SIZE]u8 = [_]u8{0xAB} ** util.HASH_SIZE;

fn writeHeader(buf: *util.Buf(u8), a: std.mem.Allocator) !void {
    try util.writeIntLe(u32, a, buf, util.MAGIC);
    try util.writeIntLe(u32, a, buf, util.FILE_VERSION);
    try buf.appendSlice(a, &hash);
}

fn writeNode(buf: *util.Buf(u8), a: std.mem.Allocator, type_: u8, id: i32, fanout: []const i32) !void {
    try buf.append(a, type_);
    try util.writeIntLe(i32, a, buf, id);
    try util.writeIntLe(i32, a, buf, @intCast(fanout.len));
    for (fanout) |f| try util.writeIntLe(i32, a, buf, f);
}

fn buildWiring(
    a: std.mem.Allocator,
    input_ports: []const [3]i32,
    output_ports: []const [2]i32,
    lamps: []const [3]i32,
    gates: []const [3]i32,
    wires: []const [3]i32,
) ![]const u8 {
    var buf = try util.Buf(u8).init(a, 4096);
    try writeHeader(&buf, a);
    try util.writeIntLe(i32, a, &buf, 6);
    const table = buf.len;
    for (0..6) |_| try util.writeIntLe(u32, a, &buf, 0);
    var starts: [6]u32 = undefined;

    starts[0] = @intCast(buf.len);
    try util.writeIntLe(i32, a, &buf, @intCast(input_ports.len));
    for (input_ports) |n| try writeNode(&buf, a, @intCast(n[0]), n[1], &.{n[2]});
    starts[1] = @intCast(buf.len);
    try util.writeIntLe(i32, a, &buf, @intCast(output_ports.len));
    for (output_ports) |n| try writeNode(&buf, a, @intCast(n[0]), n[1], &.{});
    starts[2] = @intCast(buf.len);
    try util.writeIntLe(i32, a, &buf, @intCast(lamps.len));
    for (lamps) |n| try writeNode(&buf, a, @intCast(n[0]), n[1], &.{n[2]});
    starts[3] = @intCast(buf.len);
    try util.writeIntLe(i32, a, &buf, @intCast(gates.len));
    for (gates) |n| try writeNode(&buf, a, @intCast(n[0]), n[1], &.{n[2]});
    starts[4] = @intCast(buf.len);
    try util.writeIntLe(i32, a, &buf, @intCast(wires.len));
    for (wires) |n| try writeNode(&buf, a, @intCast(n[0]), n[1], &.{n[2]});
    starts[5] = @intCast(buf.len);

    for (starts, 0..) |st, i| {
        std.mem.writeInt(u32, buf.items[table + i * 4 ..][0..4], st, .little);
    }
    return buf.slice();
}

fn buildIo(a: std.mem.Allocator) ![]const u8 {
    var buf = try util.Buf(u8).init(a, 512);
    try writeHeader(&buf, a);
    try util.writeIntLe(i32, a, &buf, 8);
    const table = buf.len;
    for (0..8) |_| try util.writeIntLe(u32, a, &buf, 0);
    var starts: [8]u32 = undefined;
    for (0..8) |i| {
        starts[i] = @intCast(buf.len);
        try util.writeIntLe(i32, a, &buf, 0);
    }
    for (starts, 0..) |st, i| {
        std.mem.writeInt(u32, buf.items[table + i * 4 ..][0..4], st, .little);
    }
    return buf.slice();
}

const Mock = struct {
    a: std.mem.Allocator,
    fd: std.posix.socket_t,
    buf: util.Buf(u8),
    send_id: i64,
    last_id: i64,

    fn request(self: *Mock, t: protocol.Tag, body: []const u8) !Ack {
        self.send_id += 1;
        try protocol.writeMessage(self.fd, t, self.send_id, body);

        const msg = (try protocol.readMessage(self.fd, self.a, &self.buf)) orelse
            return error.ConnectionClosed;
        try testing.expectEqual(protocol.ackTagOf(t), msg.tag);
        self.last_id += 1;
        try testing.expectEqual(self.last_id, msg.id);

        var r = util.Reader.init(msg.body);
        return .{
            .status = try r.readI32(),
            .message = try r.readString(),
            .payload = try r.readBytes(r.remaining()),
        };
    }

    fn syncTo(self: *Mock, world_path: []const u8) !Ack {
        var body = try util.Buf(u8).init(self.a, 256);
        try body.appendSlice(self.a, &hash);
        try util.writeString(self.a, &body, world_path);
        return self.request(.sync_to, body.slice());
    }

    fn startup(self: *Mock) !Ack {
        return self.request(.startup, &.{});
    }

    fn frame(self: *Mock, run: bool, tick: i64, inputs: []const [2]i32) !Ack {
        var body = try util.Buf(u8).init(self.a, 256);
        try body.append(self.a, @intFromBool(run));
        try util.writeIntLe(i64, self.a, &body, tick);
        try util.writeIntLe(i32, self.a, &body, @intCast(inputs.len));
        for (inputs) |in| {
            try util.writeIntLe(i32, self.a, &body, in[0]);
            try util.writeIntLe(i32, self.a, &body, in[1]);
        }
        return self.request(.frame, body.slice());
    }

    fn reset(self: *Mock) !Ack {
        return self.request(.reset, &.{});
    }

    fn syncFrom(self: *Mock) !Ack {
        return self.request(.sync_from, &.{});
    }

    fn shutdown(self: *Mock) !Ack {
        return self.request(.shutdown, &.{});
    }

    const Ack = struct {
        status: i32,
        message: []const u8,
        payload: []const u8,
    };
};

fn unpackOutputs(a: std.mem.Allocator, payload: []const u8) ![]const [2]i32 {
    var r = util.Reader.init(payload);
    const count = try r.readI32();
    const out = try a.alloc([2]i32, @intCast(count));
    for (out) |*o| {
        o[0] = try r.readI32();
        o[1] = try r.readI32();
    }
    return out;
}

const Session = struct {
    a: std.mem.Allocator,
    io: std.Io,
    sock_path: []u8,
    world_path: []u8,
    child: std.process.Child,
    mock: Mock,

    fn deinit(self: *Session) void {
        util.closeFd(self.mock.fd);
        self.child.kill(self.io); // kill blocks until termination and cleans up
    }
};

fn bindListener(a: std.mem.Allocator, sock_path: []const u8) !std.posix.socket_t {
    _ = a;
    const fd = socketUnix() catch return error.SocketFailed;

    var addr: std.posix.sockaddr.un = undefined;
    addr.family = std.posix.AF.UNIX;
    const path_len = @min(sock_path.len, addr.path.len - 1);
    @memcpy(addr.path[0..path_len], sock_path[0..path_len]);
    @memset(addr.path[path_len..], 0);
    const addr_len: std.posix.socklen_t = @intCast(@offsetOf(std.posix.sockaddr.un, "path") + path_len + 1);

    const brc = std.posix.system.bind(fd, @ptrCast(&addr), addr_len);
    if (std.posix.errno(brc) != .SUCCESS) {
        util.closeFd(fd);
        return error.BindFailed;
    }
    const lrc = std.posix.system.listen(fd, 8);
    if (std.posix.errno(lrc) != .SUCCESS) {
        util.closeFd(fd);
        return error.ListenFailed;
    }
    return fd;
}

fn socketUnix() error{SocketFailed}!std.posix.socket_t {
    const rc = std.posix.system.socket(std.posix.AF.UNIX, std.posix.SOCK.STREAM, 0);
    return switch (std.posix.errno(rc)) {
        .SUCCESS => @intCast(rc),
        else => error.SocketFailed,
    };
}

fn acceptWithDeadline(listen_fd: std.posix.socket_t, timeout_ms: i32) !std.posix.socket_t {
    var fds = [_]std.posix.pollfd{.{ .fd = listen_fd, .events = std.posix.POLL.IN, .revents = 0 }};
    const n = try std.posix.poll(&fds, timeout_ms);
    if (n == 0) return error.Timeout;
    const rc = std.posix.system.accept(listen_fd, null, null);
    return switch (std.posix.errno(rc)) {
        .SUCCESS => @intCast(rc),
        else => error.AcceptFailed,
    };
}

/// Write the graph files and spawn a backend; accept its connection as the
/// mock frontend. The backend is the connecting client, so it can be
/// started before the pipe exists.
fn startSession(a: std.mem.Allocator, wiring: []const u8, io_file: []const u8) !Session {
    var tmp = testing.tmpDir(.{});
    errdefer tmp.cleanup();
    const sock_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/pipe.sock", .{&tmp.sub_path});
    const world_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/TestWorld.wld", .{&tmp.sub_path});
    const wwir_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/TestWorld.wwir", .{&tmp.sub_path});
    const wwio_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/TestWorld.wwio", .{&tmp.sub_path});

    var threaded = std.Io.Threaded.init(std.heap.page_allocator, .{});
    defer threaded.deinit();
    const io = threaded.io();
    for ([_][]const u8{ wwir_path, wwio_path }) |p| {
        const f = try std.Io.Dir.createFileAbsolute(io, p, .{ .truncate = true });
        const data = if (std.mem.endsWith(u8, p, "wwir")) wiring else io_file;
        try std.Io.File.writePositionalAll(f, io, data, 0);
    }

    const listen_fd = try bindListener(a, sock_path);
    defer util.closeFd(listen_fd);

    const bin_path = blk: {
        const ptr = std.c.getenv("MACHA_BIN") orelse return error.NoMachaBin;
        break :blk try a.dupe(u8, std.mem.span(ptr));
    };
    var child = try std.process.spawn(io, .{
        .argv = &.{ bin_path, "serve", "--pipe", sock_path },
        .stdout = .ignore,
        .stderr = .ignore,
    });

    const conn = acceptWithDeadline(listen_fd, 15_000) catch {
        child.kill(io); // kill blocks until termination and cleans up
        return error.BackendDidNotConnect;
    };

    return .{
        .a = a,
        .io = io,
        .sock_path = sock_path,
        .world_path = world_path,
        .child = child,
        .mock = .{
            .a = a,
            .fd = conn,
            .buf = try util.Buf(u8).init(a, 4096),
            .send_id = 0,
            .last_id = 0,
        },
    };
}

fn andGateFiles(a: std.mem.Allocator) !struct { wiring: []const u8, io: []const u8 } {
    const wiring = try buildWiring(
        a,
        &.{ .{ 1, 0, 6 }, .{ 1, 1, 7 } },
        &.{.{ 1, 2 }},
        &.{ .{ 2, 3, 5 }, .{ 2, 4, 5 } },
        &.{.{ 1, 5, 8 }},
        &.{ .{ 1, 6, 3 }, .{ 1, 7, 4 }, .{ 1, 8, 2 } },
    );
    return .{ .wiring = wiring, .io = try buildIo(a) };
}

fn faultGateFiles(a: std.mem.Allocator) !struct { wiring: []const u8, io: []const u8 } {
    const wiring = try buildWiring(
        a,
        &.{ .{ 1, 0, 6 }, .{ 1, 1, 7 } },
        &.{.{ 1, 2 }},
        &.{ .{ 2, 3, 5 }, .{ 3, 4, 5 } },
        &.{.{ 7, 5, 8 }},
        &.{ .{ 1, 6, 3 }, .{ 1, 7, 4 }, .{ 1, 8, 2 } },
    );
    return .{ .wiring = wiring, .io = try buildIo(a) };
}

test "full session with AND gate" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const files = try andGateFiles(a);
    var s = try startSession(a, files.wiring, files.io);
    defer s.deinit();

    var ack = try s.mock.syncTo(s.world_path);
    try testing.expectEqual(@as(i32, 0), ack.status);

    ack = try s.mock.startup();
    try testing.expectEqual(@as(i32, 0), ack.status);

    ack = try s.mock.frame(true, 0, &.{.{ 0, 1 }});
    try testing.expectEqual(@as(i32, 0), ack.status);
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 1, &.{.{ 1, 1 }});
    try testing.expectEqual(@as(i32, 0), ack.status);
    try testing.expectEqualSlices([2]i32, &.{.{ 0, 1 }}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 2, &.{.{ 0, 1 }});
    try testing.expectEqualSlices([2]i32, &.{.{ 0, 1 }}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 3, &.{.{ 1, 1 }});
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 4, &.{.{ 0, 2 }});
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(false, 5, &.{.{ 0, 3 }});
    try testing.expectEqual(@as(i32, 0), ack.status);
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.reset();
    try testing.expectEqual(@as(i32, 0), ack.status);

    ack = try s.mock.frame(true, 6, &.{.{ 0, 1 }});
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));
    ack = try s.mock.frame(true, 7, &.{.{ 1, 1 }});
    try testing.expectEqualSlices([2]i32, &.{.{ 0, 1 }}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.syncFrom();
    try testing.expectEqual(@as(i32, 0), ack.status);
    var r = util.Reader.init(ack.payload);
    try testing.expectEqualSlices(u8, &hash, try r.readBytes(util.HASH_SIZE));
    try testing.expectEqualStrings(s.world_path, try r.readString());

    ack = try s.mock.shutdown();
    try testing.expectEqual(@as(i32, 0), ack.status);
}

test "full session with fault gate" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const files = try faultGateFiles(a);
    var s = try startSession(a, files.wiring, files.io);
    defer s.deinit();

    try testing.expectEqual(@as(i32, 0), (try s.mock.syncTo(s.world_path)).status);
    try testing.expectEqual(@as(i32, 0), (try s.mock.startup()).status);

    var ack = try s.mock.frame(true, 0, &.{.{ 0, 1 }});
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 1, &.{.{ 1, 1 }});
    try testing.expectEqualSlices([2]i32, &.{.{ 0, 1 }}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 2, &.{.{ 0, 1 }});
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    ack = try s.mock.frame(true, 3, &.{.{ 1, 1 }});
    try testing.expectEqualSlices([2]i32, &.{}, try unpackOutputs(a, ack.payload));

    try testing.expectEqual(@as(i32, 0), (try s.mock.shutdown()).status);
}

test "sync_to rejects hash mismatch" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const files = try andGateFiles(a);
    var s = try startSession(a, files.wiring, files.io);
    defer s.deinit();

    var body = try util.Buf(u8).init(a, 256);
    try body.appendSlice(a, &([_]u8{0x11} ** util.HASH_SIZE));
    try util.writeString(a, &body, s.world_path);
    var ack = try s.mock.request(.sync_to, body.slice());
    try testing.expectEqual(@as(i32, 1), ack.status);
    try testing.expect(std.mem.indexOf(u8, ack.message, "mismatch") != null);

    ack = try s.mock.startup();
    try testing.expectEqual(@as(i32, 1), ack.status);
    try testing.expect(std.mem.indexOf(u8, ack.message, "synced") != null);

    try testing.expectEqual(@as(i32, 0), (try s.mock.syncTo(s.world_path)).status);
    try testing.expectEqual(@as(i32, 0), (try s.mock.startup()).status);
    try testing.expectEqual(@as(i32, 0), (try s.mock.shutdown()).status);
}

test "backend rejects id gap without a reply" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const files = try andGateFiles(a);
    var s = try startSession(a, files.wiring, files.io);
    defer s.deinit();

    try testing.expectEqual(@as(i32, 0), (try s.mock.syncTo(s.world_path)).status);
    try testing.expectEqual(@as(i32, 0), (try s.mock.startup()).status);

    // Send a request with a skipped message id (expect 3, send 4): the
    // backend must detect the gap and drop the connection without a reply.
    var body = try util.Buf(u8).init(a, 64);
    try body.append(a, 1);
    try util.writeIntLe(i64, a, &body, 0);
    try util.writeIntLe(i32, a, &body, 0);
    try protocol.writeMessage(s.mock.fd, .frame, 4, body.slice());

    // Wait for readability, then the read must hit a clean EOF.
    var fds = [_]std.posix.pollfd{.{ .fd = s.mock.fd, .events = std.posix.POLL.IN, .revents = 0 }};
    const n = try std.posix.poll(&fds, 5000);
    try testing.expect(n > 0);
    try testing.expectEqual(@as(?protocol.Message, null), try protocol.readMessage(s.mock.fd, a, &s.mock.buf));
}

test "startup before sync fails" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const files = try andGateFiles(a);
    var s = try startSession(a, files.wiring, files.io);
    defer s.deinit();

    var ack = try s.mock.startup();
    try testing.expectEqual(@as(i32, 1), ack.status);
    try testing.expect(std.mem.indexOf(u8, ack.message, "synced") != null);

    ack = try s.mock.frame(true, 0, &.{.{ 0, 1 }});
    try testing.expectEqual(@as(i32, 1), ack.status);

    try testing.expectEqual(@as(i32, 0), (try s.mock.shutdown()).status);
}

test "backend connects again for a second session" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const files = try andGateFiles(a);
    {
        var s1 = try startSession(a, files.wiring, files.io);
        s1.deinit();
    }
    var s2 = try startSession(a, files.wiring, files.io);
    defer s2.deinit();
    try testing.expectEqual(@as(i32, 0), (try s2.mock.syncTo(s2.world_path)).status);
    try testing.expectEqual(@as(i32, 0), (try s2.mock.startup()).status);
    try testing.expectEqual(@as(i32, 0), (try s2.mock.shutdown()).status);
}

test "out_buf stays owned by the session allocator (70 outputs)" {
    // Regression for the cross-allocator realloc bug: a frame with more
    // outputs than the initial out_buf capacity (64) used to realloc the
    // session-owned buffer with the sim's world-arena allocator, leaving a
    // dangling pointer after re-sync and a double free at deinit.
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    var wbuf = try util.Buf(u8).init(a, 4096);
    try writeHeader(&wbuf, a);
    try util.writeIntLe(i32, a, &wbuf, 6);
    const table = wbuf.len;
    for (0..6) |_| try util.writeIntLe(u32, a, &wbuf, 0);
    var starts: [6]u32 = undefined;

    starts[0] = @intCast(wbuf.len);
    try util.writeIntLe(i32, a, &wbuf, 1); // 1 input port
    try writeNode(&wbuf, a, 1, 0, &.{71});
    starts[1] = @intCast(wbuf.len);
    try util.writeIntLe(i32, a, &wbuf, 70); // 70 output ports
    for (0..70) |i| try writeNode(&wbuf, a, 1, @intCast(1 + i), &.{});
    starts[2] = @intCast(wbuf.len);
    try util.writeIntLe(i32, a, &wbuf, 0); // no lamps
    starts[3] = @intCast(wbuf.len);
    try util.writeIntLe(i32, a, &wbuf, 0); // no gates
    starts[4] = @intCast(wbuf.len);
    try util.writeIntLe(i32, a, &wbuf, 1); // 1 wire → all 70 outputs
    var fan: [70]i32 = undefined;
    for (&fan, 0..) |*f, i| f.* = @intCast(1 + i);
    try writeNode(&wbuf, a, 1, 71, &fan);
    starts[5] = @intCast(wbuf.len);
    for (starts, 0..) |st, i| {
        std.mem.writeInt(u32, wbuf.items[table + i * 4 ..][0..4], st, .little);
    }

    const iofile = try buildIo(a);

    var tmp = testing.tmpDir(.{});
    defer tmp.cleanup();
    const world_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/Fanout.wld", .{&tmp.sub_path});
    const wwir_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/Fanout.wwir", .{&tmp.sub_path});
    const wwio_path = try std.fmt.allocPrint(a, ".zig-cache/tmp/{s}/Fanout.wwio", .{&tmp.sub_path});

    var threaded = std.Io.Threaded.init(std.heap.page_allocator, .{});
    defer threaded.deinit();
    const io = threaded.io();
    for ([_][]const u8{ wwir_path, wwio_path }) |p| {
        const f = try std.Io.Dir.createFileAbsolute(io, p, .{ .truncate = true });
        const data = if (std.mem.endsWith(u8, p, "wwir")) wbuf.slice() else iofile;
        try std.Io.File.writePositionalAll(f, io, data, 0);
    }

    var s = try macha.Session.init(a);
    defer s.deinit();

    var sync_body = try util.Buf(u8).init(a, 256);
    try sync_body.appendSlice(a, &hash);
    try util.writeString(a, &sync_body, world_path);
    var ack = s.dispatch(io, .sync_to, sync_body.slice());
    try testing.expectEqual(@as(i32, 0), ack.status);

    var frame = try util.Buf(u8).init(a, 256);
    try frame.append(a, 1); // run
    try util.writeIntLe(i64, a, &frame, 0); // tick
    try util.writeIntLe(i32, a, &frame, 1); // one input
    try util.writeIntLe(i32, a, &frame, 0); // port 0
    try util.writeIntLe(i32, a, &frame, 1); // count 1
    ack = s.dispatch(io, .frame, frame.slice());
    try testing.expectEqual(@as(i32, 0), ack.status);
    try testing.expectEqual(@as(usize, 70), (try unpackOutputs(a, ack.payload)).len);

    // Re-sync (releases the world arena) and frame again: under the old bug
    // out_buf pointed into the freed arena and this realloc'd freed memory.
    ack = s.dispatch(io, .sync_to, sync_body.slice());
    try testing.expectEqual(@as(i32, 0), ack.status);
    ack = s.dispatch(io, .frame, frame.slice());
    try testing.expectEqual(@as(i32, 0), ack.status);
    try testing.expectEqual(@as(usize, 70), (try unpackOutputs(a, ack.payload)).len);
}
