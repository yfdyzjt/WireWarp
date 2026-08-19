const std = @import("std");
const macha = @import("macha");
const protocol = macha.protocol;
const util = macha.util;

const hash_size = util.HASH_SIZE;

const Mock = struct {
    fd: std.posix.socket_t,
    a: std.mem.Allocator,
    buf: util.Buf(u8),
    send_id: i64,
    last_id: i64,

    fn request(self: *Mock, t: protocol.Tag, body: []const u8) !void {
        self.send_id += 1;
        try protocol.writeMessage(self.fd, t, self.send_id, body);
        const msg = (try protocol.readMessage(self.fd, self.a, &self.buf)) orelse
            return error.ConnectionClosed;
        if (msg.tag != protocol.ackTagOf(t)) return error.BadAckTag;
        self.last_id += 1;
        if (msg.id != self.last_id) return error.BadAckId;
        var r = util.Reader.init(msg.body);
        if (try r.readI32() != 0) return error.RequestFailed;
    }

    fn syncTo(self: *Mock, world_hash: *const [hash_size]u8, world_path: []const u8) !void {
        var body = try util.Buf(u8).init(self.a, 512);
        try body.appendSlice(self.a, world_hash);
        try util.writeString(self.a, &body, world_path);
        try self.request(.sync_to, body.slice());
    }
};

fn socketUnix() error{SocketFailed}!std.posix.socket_t {
    const rc = std.posix.system.socket(std.posix.AF.UNIX, std.posix.SOCK.STREAM, 0);
    return switch (std.posix.errno(rc)) {
        .SUCCESS => @intCast(rc),
        else => error.SocketFailed,
    };
}

fn bindListener(sock_path: []const u8) !std.posix.socket_t {
    const fd = socketUnix() catch return error.SocketFailed;

    var addr: std.posix.sockaddr.un = undefined;
    addr.family = std.posix.AF.UNIX;
    const path_len = @min(sock_path.len, addr.path.len - 1);
    @memcpy(addr.path[0..path_len], sock_path[0..path_len]);
    @memset(addr.path[path_len..], 0);
    const addr_len: std.posix.socklen_t = @intCast(@offsetOf(std.posix.sockaddr.un, "path") + path_len + 1);

    if (std.posix.errno(std.posix.system.bind(fd, @ptrCast(&addr), addr_len)) != .SUCCESS) {
        util.closeFd(fd);
        return error.BindFailed;
    }
    if (std.posix.errno(std.posix.system.listen(fd, 8)) != .SUCCESS) {
        util.closeFd(fd);
        return error.ListenFailed;
    }
    return fd;
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

fn readFile(io: std.Io, a: std.mem.Allocator, path: []const u8) ![]u8 {
    return std.Io.Dir.cwd().readFileAlloc(io, path, a, .unlimited);
}

pub fn main(init: std.process.Init) !void {
    const io = init.io;
    const arena = init.arena.allocator();
    const args = try init.minimal.args.toSlice(arena);
    if (args.len < 2) {
        std.debug.print("usage: frontend-bench <world.wld> [events]\n", .{});
        return;
    }
    const world_path: []const u8 = args[1];
    const events: u64 = if (args.len > 2) std.fmt.parseInt(u64, args[2], 10) catch 100_000 else 100_000;

    const wwir_path = try macha.files.withExt(arena, world_path, "wwir");
    const wwio_path = try macha.files.withExt(arena, world_path, "wwio");
    const wwir = try readFile(io, arena, wwir_path);
    const wwio = try readFile(io, arena, wwio_path);

    var world_hash: [hash_size]u8 = undefined;
    @memcpy(world_hash[0..], wwir[8 .. 8 + hash_size]);

    const iof = try macha.files.parseIo(arena, wwio);
    const plate_type: u8 = 5; // InputID.ProjectilePressurePad: the clock sources
    var plates = try util.Buf(i32).init(arena, 64);
    var others = try util.Buf(i32).init(arena, 64);
    for (iof.inputs) |in| {
        if (in.type_ == plate_type)
            try plates.append(arena, in.port_id)
        else
            try others.append(arena, in.port_id);
    }
    if (plates.items.len == 0 and others.items.len == 0) return error.NoInputs;

    const bin_path = std.process.Environ.getAlloc(init.minimal.environ, arena, "MACHA_BIN") catch
        return error.NoMachaBin;
    const sock_path = std.fmt.allocPrint(arena, "/tmp/macha-bench-{d}.sock", .{std.Io.Clock.now(.awake, io).nanoseconds}) catch unreachable;
    defer std.Io.Dir.deleteFileAbsolute(io, sock_path) catch {};

    const listen_fd = try bindListener(sock_path);
    defer util.closeFd(listen_fd);

    var child = try std.process.spawn(io, .{
        .argv = &.{ bin_path, "serve", "--pipe", sock_path },
        .stdout = .ignore,
        .stderr = .ignore,
    });
    defer child.kill(io);

    const conn = try acceptWithDeadline(listen_fd, 15_000);
    defer util.closeFd(conn);

    var mock = Mock{
        .fd = conn,
        .a = arena,
        .buf = try util.Buf(u8).init(arena, 4096),
        .send_id = 0,
        .last_id = 0,
    };
    try mock.syncTo(&world_hash, world_path);
    try mock.request(.startup, &.{});

    var rng = std.Random.DefaultPrng.init(42);
    var frame_buf = try util.Buf(u8).init(arena, 64);
    const latencies = try arena.alloc(u64, events);
    var outputs: u64 = 0;

    var i: u64 = 0;
    while (i < events) : (i += 1) {
        const want_plate = rng.random().uintLessThan(u8, 100) < 99;
        const pool: []const i32 = if (want_plate and plates.items.len > 0)
            plates.slice()
        else if (!want_plate and others.items.len > 0)
            others.slice()
        else if (plates.items.len > 0)
            plates.slice()
        else
            others.slice();
        const port = pool[rng.random().uintLessThan(usize, pool.len)];

        frame_buf.clear();
        try frame_buf.append(arena, 1); // run
        try util.writeIntLe(i64, arena, &frame_buf, @intCast(i)); // tick
        try util.writeIntLe(i32, arena, &frame_buf, 1); // one input
        try util.writeIntLe(i32, arena, &frame_buf, port);
        try util.writeIntLe(i32, arena, &frame_buf, 1);

        const t0 = std.Io.Clock.now(.awake, io);
        mock.send_id += 1;
        try protocol.writeMessage(conn, .frame, mock.send_id, frame_buf.slice());
        const msg = (try protocol.readMessage(conn, arena, &mock.buf)) orelse
            return error.ConnectionClosed;
        const t1 = std.Io.Clock.now(.awake, io);
        latencies[i] = @intCast(t1.nanoseconds - t0.nanoseconds);

        if (msg.tag != .frame_ack) return error.BadAckTag;
        mock.last_id += 1;
        if (msg.id != mock.last_id) return error.BadAckId;
        var r = util.Reader.init(msg.body);
        if (try r.readI32() != 0) return error.RequestFailed;
        _ = try r.readString();
        const pairs = try r.readI32();
        var p: i32 = 0;
        while (p < pairs) : (p += 1) {
            _ = try r.readI32();
            outputs += @intCast(try r.readI32());
        }
    }

    std.mem.sortUnstable(u64, latencies, {}, lessThan);
    const total = sum(latencies);
    const mean: f64 = @as(f64, @floatFromInt(total)) / @as(f64, @floatFromInt(events));
    std.debug.print("events: {d}, outputs: {d}\n", .{ events, outputs });
    std.debug.print(
        "round-trip: total {d} ms ({d} ns/event mean) | p50 {d} ns | p90 {d} ns | p99 {d} ns | max {d} ns\n",
        .{
            total / std.time.ns_per_ms,
            mean,
            latencies[events / 2],
            latencies[events * 9 / 10],
            latencies[events * 99 / 100],
            latencies[events - 1],
        },
    );
    std.debug.print("in-process sim reference: ~43 us/event (no pipe)\n", .{});
}

fn lessThan(_: void, a: u64, b: u64) bool {
    return a < b;
}

fn sum(v: []const u64) u64 {
    var total: u64 = 0;
    for (v) |x| total += x;
    return total;
}
