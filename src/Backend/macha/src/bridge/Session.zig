const std = @import("std");
const util = @import("../util.zig");
const files = @import("files.zig");
const Graph = @import("../core/Graph.zig");
const compiler = @import("../core/compiler.zig");
const Sim = @import("../core/Sim.zig");
const protocol = @import("protocol.zig");
const Session = @This();

const hash_size = util.HASH_SIZE;

pub const Error = error{
    NotSynced,
    BadSyncBody,
    WiringHashMismatch,
    IoHashMismatch,
    HashMismatch,
    FileRead,
    FileMagic,
    FileVersion,
    BadWorldFiles,
    BadFrameBody,
    BadCount,
    UnknownTag,
} || std.mem.Allocator.Error;

alloc: std.mem.Allocator,
loaded: ?struct {
    arena: std.heap.ArenaAllocator,
    hash: [hash_size]u8,
    path: []u8,
    graph: Graph,
    sim: Sim,
    warned_ports: std.AutoHashMapUnmanaged(i32, void),
} = null,

frame_out: util.Buf(u8), // RLE-packed Frame acknowledgment payload
out_buf: util.Buf(i32), // per-frame raw output port ids
pairs_buf: util.Buf(u64), // (port, count) run pairs while packing
sync_from_buf: util.Buf(u8),

pub const Ack = struct {
    status: i32,
    message: []const u8,
    payload: []const u8,
};

pub fn init(a: std.mem.Allocator) std.mem.Allocator.Error!Session {
    return .{
        .alloc = a,
        .frame_out = try util.Buf(u8).init(a, 256),
        .out_buf = try util.Buf(i32).init(a, 64),
        .pairs_buf = try util.Buf(u64).init(a, 64),
        .sync_from_buf = try util.Buf(u8).init(a, 64),
    };
}

pub fn deinit(self: *Session) void {
    self.shutdown();
    self.frame_out.deinit(self.alloc);
    self.out_buf.deinit(self.alloc);
    self.pairs_buf.deinit(self.alloc);
    self.sync_from_buf.deinit(self.alloc);
}

pub fn isSynced(self: *const Session) bool {
    return self.loaded != null;
}

pub fn syncTo(self: *Session, io: std.Io, body: []const u8) Error!void {
    const sync = parseSyncBody(body) catch return error.BadSyncBody;

    var scratch = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    defer scratch.deinit();
    const t = scratch.allocator();

    const wwir_path = try files.withExt(t, sync.path, "wwir");
    const wwio_path = try files.withExt(t, sync.path, "wwio");
    const wwir = std.Io.Dir.cwd().readFileAlloc(io, wwir_path, t, .unlimited) catch
        return error.FileRead;
    const wwio = std.Io.Dir.cwd().readFileAlloc(io, wwio_path, t, .unlimited) catch
        return error.FileRead;
    verifyHeader(wwir, sync.hash) catch |e| {
        if (e == error.HashMismatch) return error.WiringHashMismatch;
        return e;
    };
    verifyHeader(wwio, sync.hash) catch |e| {
        if (e == error.HashMismatch) return error.IoHashMismatch;
        return e;
    };

    const wiring = files.parseWiring(t, wwir) catch return error.BadWorldFiles;
    _ = files.parseIo(t, wwio) catch return error.BadWorldFiles;

    var world_arena = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    const wa = world_arena.allocator();
    const g = compiler.compile(wa, &wiring) catch {
        world_arena.deinit();
        return error.BadWorldFiles;
    };
    const path_copy = wa.dupe(u8, sync.path) catch |e| {
        world_arena.deinit();
        return e;
    };

    self.releaseLoaded();
    self.loaded = .{
        .arena = world_arena,
        .hash = sync.hash,
        .path = path_copy,
        .graph = g,
        .sim = undefined,
        .warned_ports = .empty,
    };

    // The arena now lives in `self.loaded`; derive the sim's allocator from
    // the stored arena, not from the local, or the sim would hold a dangling
    // pointer to the stack frame once syncTo returns.
    const seed: u64 = @truncate(@as(u96, @bitCast(std.Io.Clock.now(.awake, io).nanoseconds)));
    const stored_a = self.loaded.?.arena.allocator();
    self.loaded.?.sim = Sim.init(stored_a, &self.loaded.?.graph, seed) catch |e| {
        self.loaded.?.arena.deinit();
        self.loaded = null;
        return e;
    };

    std.debug.print("synced to world ({d} wires, {d} lamps, {d} gates, {d} inputs)\n", .{
        wiring.wires.len,
        wiring.lamps.len,
        wiring.gates.len,
        wiring.input_ports.len,
    });
}

pub fn startup(self: *Session) Error!void {
    if (self.loaded == null) return error.NotSynced;
}

pub fn syncFrom(self: *Session) Error![]const u8 {
    if (self.loaded == null) return error.NotSynced;
    const l = &self.loaded.?;
    self.sync_from_buf.clear();
    try self.sync_from_buf.appendSlice(self.alloc, l.hash[0..]);
    try util.writeString(self.alloc, &self.sync_from_buf, l.path);
    return self.sync_from_buf.slice();
}

pub fn reset(self: *Session) Error!void {
    if (self.loaded == null) return error.NotSynced;
    const l = &self.loaded.?;
    l.sim.reset();
    l.warned_ports.clearRetainingCapacity();
}

pub fn frame(self: *Session, body: []const u8) Error![]const u8 {
    const req = try parseFrameBody(self.alloc, body);
    defer self.alloc.free(req.inputs);
    self.out_buf.clear();

    if (req.run) {
        if (self.loaded == null) return error.NotSynced;
        const l = &self.loaded.?;
        for (req.inputs) |in| {
            const port = in[0];
            if (port >= 0 and port < @as(i32, @intCast(l.graph.input_ports.len))) {
                var k: i32 = 0;
                while (k < in[1]) : (k += 1) {
                    try self.out_buf.appendSlice(self.alloc, try l.sim.event(port));
                }
            } else if (!l.warned_ports.contains(port)) {
                try l.warned_ports.put(self.alloc, port, {});
                std.debug.print("frame tick {d}: input port {d} not found in graph, ignoring\n", .{ req.tick, port });
            }
        }
    } else if (req.inputs.len > 0) {
        std.debug.print("frame tick {d}: paused, ignoring {d} input events\n", .{ req.tick, req.inputs.len });
    }

    try packOutputsRle(self.alloc, &self.frame_out, &self.pairs_buf, self.out_buf.slice());
    return self.frame_out.slice();
}

pub fn dispatch(self: *Session, io: std.Io, t: protocol.Tag, body: []const u8) Ack {
    var payload: []const u8 = &.{};
    var err: ?anyerror = null;
    switch (t) {
        .startup => self.startup() catch |e| {
            err = e;
        },
        .sync_to => self.syncTo(io, body) catch |e| {
            err = e;
        },
        .sync_from => {
            if (self.syncFrom()) |p| payload = p else |e| err = e;
        },
        .reset => self.reset() catch |e| {
            err = e;
        },
        .frame => {
            if (self.frame(body)) |p| payload = p else |e| err = e;
        },
        .shutdown => self.shutdown(),
        else => err = error.UnknownTag,
    }
    if (err) |e| return .{ .status = 1, .message = messageOf(e), .payload = &.{} };
    return .{ .status = 0, .message = "", .payload = payload };
}

pub fn shutdown(self: *Session) void {
    self.releaseLoaded();
}

fn releaseLoaded(self: *Session) void {
    if (self.loaded) |*loaded| {
        loaded.warned_ports.deinit(self.alloc);
        loaded.arena.deinit();
        self.loaded = null;
    }
}

pub fn messageOf(err: anyerror) []const u8 {
    return switch (err) {
        error.NotSynced => "backend not synced, SyncTo must precede this request",
        error.BadSyncBody => "invalid SyncTo payload",
        error.WiringHashMismatch => "wiring file hash mismatch",
        error.IoHashMismatch => "io file hash mismatch",
        error.HashMismatch => "file hash mismatch",
        error.FileRead => "cannot read world file",
        error.FileMagic => "world file header magic mismatch",
        error.FileVersion => "world file header version mismatch",
        error.BadWorldFiles => "cannot parse or compile world files",
        error.FaultGateWithoutLamp => "fault gate without parameter lamp",
        error.BadFrameBody => "invalid Frame payload",
        error.BadCount => "invalid Frame payload count",
        error.UnknownTag => "unknown request tag",
        else => @errorName(err),
    };
}

fn parseSyncBody(body: []const u8) Error!struct {
    hash: [hash_size]u8,
    path: []const u8,
} {
    var r = util.Reader.init(body);
    var hash: [hash_size]u8 = undefined;
    @memcpy(hash[0..], r.readBytes(hash_size) catch return error.BadSyncBody);
    const path = r.readString() catch return error.BadSyncBody;
    return .{ .hash = hash, .path = path };
}

fn parseFrameBody(a: std.mem.Allocator, body: []const u8) Error!struct {
    run: bool,
    tick: i64,
    inputs: []const [2]i32,
} {
    var r = util.Reader.init(body);
    const run = r.readU8() catch return error.BadFrameBody;
    const tick = r.readI64() catch return error.BadFrameBody;
    const count = r.readI32() catch return error.BadFrameBody;
    if (count < 0 or count > 16_000_000) return error.BadCount;
    const inputs = try a.alloc([2]i32, @intCast(count));
    for (inputs) |*in| {
        in[0] = r.readI32() catch return error.BadFrameBody;
        in[1] = r.readI32() catch return error.BadFrameBody;
        if (in[1] <= 0) return error.BadCount;
    }
    return .{ .run = run != 0, .tick = tick, .inputs = inputs };
}

/// `pairs` packs port in the low 32 bits and count in the high 32 bits.
fn packOutputsRle(a: std.mem.Allocator, out: *util.Buf(u8), pairs: *util.Buf(u64), outputs: []const i32) std.mem.Allocator.Error!void {
    pairs.clear();
    for (outputs) |port| {
        if (pairs.items.len > 0) {
            const last = &pairs.items[pairs.items.len - 1];
            if (@as(i32, @bitCast(@as(u32, @truncate(@as(u64, @bitCast(last.*)))))) == port) {
                last.* += @as(u64, 1) << 32;
                continue;
            }
        }
        try pairs.append(a, @as(u64, @as(u32, @bitCast(port))) | (@as(u64, 1) << 32));
    }

    out.clear();
    try util.writeIntLe(i32, a, out, @intCast(pairs.items.len));
    for (pairs.slice()) |p| {
        try util.writeIntLe(i32, a, out, @bitCast(@as(u32, @truncate(p))));
        try util.writeIntLe(i32, a, out, @bitCast(@as(u32, @truncate(p >> 32))));
    }
}

fn verifyHeader(bytes: []const u8, expected: [hash_size]u8) Error!void {
    var r = util.Reader.init(bytes);
    const m = r.readU32() catch return error.FileMagic;
    if (m != util.MAGIC) return error.FileMagic;
    const v = r.readU32() catch return error.FileVersion;
    if (v != util.FILE_VERSION) return error.FileVersion;
    var got: [hash_size]u8 = undefined;
    @memcpy(got[0..], r.readBytes(hash_size) catch return error.FileMagic);
    if (!std.mem.eql(u8, got[0..], expected[0..])) return error.HashMismatch;
}

const testing = std.testing;

test "sync body roundtrip" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    var buf = try util.Buf(u8).init(a, 128);
    const hash = [_]u8{0xAB} ** hash_size;
    try buf.appendSlice(a, &hash);
    try util.writeString(a, &buf, "/worlds/My World.wld");
    const sync = try parseSyncBody(buf.slice());
    try testing.expectEqualSlices(u8, &hash, &sync.hash);
    try testing.expectEqualStrings("/worlds/My World.wld", sync.path);
}

test "sync body rejects truncation" {
    try testing.expectError(error.BadSyncBody, parseSyncBody(&[_]u8{0xAB} ** 10));
    try testing.expectError(error.BadSyncBody, parseSyncBody(&.{}));
}

test "frame body roundtrip" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    var buf = try util.Buf(u8).init(a, 128);
    try buf.append(a, 1); // run
    try util.writeIntLe(i64, a, &buf, 1234);
    try util.writeIntLe(i32, a, &buf, 2);
    try util.writeIntLe(i32, a, &buf, 7);
    try util.writeIntLe(i32, a, &buf, 3);
    try util.writeIntLe(i32, a, &buf, 9);
    try util.writeIntLe(i32, a, &buf, 1);
    const req = try parseFrameBody(a, buf.slice());
    try testing.expect(req.run);
    try testing.expectEqual(@as(i64, 1234), req.tick);
    try testing.expectEqual(@as(usize, 2), req.inputs.len);
    try testing.expectEqualSlices([2]i32, &.{ .{ 7, 3 }, .{ 9, 1 } }, req.inputs);
}

test "frame body rejects bad counts" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    var buf = try util.Buf(u8).init(a, 128);
    try buf.append(a, 1);
    try util.writeIntLe(i64, a, &buf, 0);
    try util.writeIntLe(i32, a, &buf, 1);
    try util.writeIntLe(i32, a, &buf, 5);
    try util.writeIntLe(i32, a, &buf, 0);
    try testing.expectError(error.BadCount, parseFrameBody(a, buf.slice()));
}

test "outputs rle" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    var out = try util.Buf(u8).init(a, 64);
    var pairs = try util.Buf(u64).init(a, 64);
    try packOutputsRle(a, &out, &pairs, &.{ 7, 7, 9, 7 });
    var r = util.Reader.init(out.slice());
    try testing.expectEqual(@as(i32, 3), try r.readI32());
    try testing.expectEqual(@as(i32, 7), try r.readI32());
    try testing.expectEqual(@as(i32, 2), try r.readI32());
    try testing.expectEqual(@as(i32, 9), try r.readI32());
    try testing.expectEqual(@as(i32, 1), try r.readI32());
    try testing.expectEqual(@as(i32, 7), try r.readI32());
    try testing.expectEqual(@as(i32, 1), try r.readI32());
}

test "unstarted requests fail cleanly" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    var s = try Session.init(arena.allocator());
    defer s.deinit();

    var threaded = std.Io.Threaded.init(std.heap.page_allocator, .{});
    defer threaded.deinit();
    const io = threaded.io();

    const ack = s.dispatch(io, .startup, &.{});
    try testing.expectEqual(@as(i32, 1), ack.status);
    try testing.expect(std.mem.indexOf(u8, ack.message, "synced") != null);

    var buf = try util.Buf(u8).init(arena.allocator(), 64);
    try buf.append(arena.allocator(), 1);
    try util.writeIntLe(i64, arena.allocator(), &buf, 0);
    try util.writeIntLe(i32, arena.allocator(), &buf, 0);
    const ack2 = s.dispatch(io, .frame, buf.slice());
    try testing.expectEqual(@as(i32, 1), ack2.status);

    const ack3 = s.dispatch(io, .shutdown, &.{});
    try testing.expectEqual(@as(i32, 0), ack3.status);
    try testing.expect(!s.isSynced());

    try testing.expect(std.mem.indexOf(u8, messageOf(error.WiringHashMismatch), "mismatch") != null);
}
