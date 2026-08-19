//! Layout (all little-endian):
//! - header: u32 magic 0xBADBEEF, u32 version 1, 32-byte SHA-256 hash;
//! - group table: i32 group_count, then `group_count` absolute file offsets
//!   (u32) where each group starts; the last offset marks the end;
//! - `.wwir` groups (6): input ports, output ports, lamps, gates, wires;
//!   node record: u8 type | i32 id | i32 fanoutCount | i32 fanoutIds...;
//! - `.wwio` groups (8): inputs, outputs, lamp positions, gate positions,
//!   teleporters, pumps, wire bulbs. Only the input records are retained:
//!   the compiled graph never needs tile positions.

const std = @import("std");
const util = @import("../util.zig");

pub const ParseError = error{
    InvalidMagic,
    InvalidVersion,
    InvalidGroupCount,
    InvalidGroupOffset,
    InvalidCount,
    InvalidLength,
    UnexpectedEof,
} || std.mem.Allocator.Error;

pub const WiringFile = struct {
    hash: [util.HASH_SIZE]u8,
    input_ports: []Node,
    output_ports: []Node,
    lamps: []Node,
    gates: []Node,
    wires: []Node,

    pub const Node = struct {
        type_: u8,
        id: i32,
        fanout: []const i32,
    };
};

pub const IoFile = struct {
    hash: [util.HASH_SIZE]u8,
    inputs: []Input,

    pub const Input = struct {
        x: i32,
        y: i32,
        port_id: i32,
        type_: u8,
    };
};

pub fn parseWiring(a: std.mem.Allocator, bytes: []const u8) ParseError!WiringFile {
    var r = util.Reader.init(bytes);
    const hash = try readHeader(&r);
    const starts = try readGroupStarts(a, &r, 6);

    const input_ports = try readNodes(a, &r);
    try expectEnd(&r, starts[1]);
    const output_ports = try readNodes(a, &r);
    try expectEnd(&r, starts[2]);
    const lamps = try readNodes(a, &r);
    try expectEnd(&r, starts[3]);
    const gates = try readNodes(a, &r);
    try expectEnd(&r, starts[4]);
    const wires = try readNodes(a, &r);
    try expectEnd(&r, starts[5]);

    return .{ .hash = hash, .input_ports = input_ports, .output_ports = output_ports, .lamps = lamps, .gates = gates, .wires = wires };
}

pub fn parseIo(a: std.mem.Allocator, bytes: []const u8) ParseError!IoFile {
    var r = util.Reader.init(bytes);
    const hash = try readHeader(&r);
    const starts = try readGroupStarts(a, &r, 8);

    const inputs = try readInputs(a, &r);
    try expectEnd(&r, starts[1]);
    try skipRecords(&r, 13);
    try expectEnd(&r, starts[2]);
    try skipRecords(&r, 12);
    try expectEnd(&r, starts[3]);
    try skipRecords(&r, 12);
    try expectEnd(&r, starts[4]);
    try skipRecords(&r, 20);
    try expectEnd(&r, starts[5]);
    try skipPumps(&r);
    try expectEnd(&r, starts[6]);
    try skipRecords(&r, 5);
    try expectEnd(&r, starts[7]);

    return .{ .hash = hash, .inputs = inputs };
}

pub fn withExt(a: std.mem.Allocator, path: []const u8, ext: []const u8) std.mem.Allocator.Error![]u8 {
    const dot = std.mem.lastIndexOfScalar(u8, path, '.');
    const slash = std.mem.lastIndexOfScalar(u8, path, '/');
    const cut = if (dot) |d| if (slash) |s| if (d > s) d else path.len else d else path.len;
    const out = try a.alloc(u8, cut + 1 + ext.len);
    @memcpy(out[0..cut], path[0..cut]);
    out[cut] = '.';
    @memcpy(out[cut + 1 ..], ext);
    return out;
}

fn readHeader(r: *util.Reader) ParseError![util.HASH_SIZE]u8 {
    const magic = try r.readU32();
    if (magic != util.MAGIC) return error.InvalidMagic;
    const version = try r.readU32();
    if (version != util.FILE_VERSION) return error.InvalidVersion;
    var hash: [util.HASH_SIZE]u8 = undefined;
    @memcpy(hash[0..], try r.readBytes(util.HASH_SIZE));
    return hash;
}

fn readGroupStarts(a: std.mem.Allocator, r: *util.Reader, expected: usize) ParseError![]u32 {
    const count = try r.readI32();
    if (count != expected) return error.InvalidGroupCount;
    const starts = try a.alloc(u32, expected);
    for (starts) |*s| s.* = try r.readU32();
    if (@as(usize, starts[0]) != r.position()) return error.InvalidGroupOffset;
    return starts;
}

fn readNodes(a: std.mem.Allocator, r: *util.Reader) ParseError![]WiringFile.Node {
    const count = try r.readI32();
    if (count < 0) return error.InvalidCount;
    const out = try a.alloc(WiringFile.Node, @intCast(count));
    for (out) |*n| {
        n.type_ = try r.readU8();
        n.id = try r.readI32();
        const fanout_count = try r.readI32();
        if (fanout_count < 0) return error.InvalidCount;
        const fanout = try a.alloc(i32, @intCast(fanout_count));
        for (fanout) |*f| f.* = try r.readI32();
        n.fanout = fanout;
    }
    return out;
}

fn expectEnd(r: *const util.Reader, expected: u32) ParseError!void {
    if (r.position() != @as(usize, expected)) return error.InvalidGroupOffset;
}

fn readInputs(a: std.mem.Allocator, r: *util.Reader) ParseError![]IoFile.Input {
    const count = try r.readI32();
    if (count < 0) return error.InvalidCount;
    const out = try a.alloc(IoFile.Input, @intCast(count));
    for (out) |*in| {
        in.x = try r.readI32();
        in.y = try r.readI32();
        in.port_id = try r.readI32();
        in.type_ = try r.readU8();
    }
    return out;
}

fn skipRecords(r: *util.Reader, bytes_per: usize) ParseError!void {
    const count = try r.readI32();
    if (count < 0) return error.InvalidCount;
    try r.skip(@as(usize, @intCast(count)) * bytes_per);
}

fn skipPumps(r: *util.Reader) ParseError!void {
    const count = try r.readI32();
    if (count < 0) return error.InvalidCount;
    for (0..@intCast(count)) |_| {
        try r.skip(4); // port id
        const inlets = try r.readI32();
        if (inlets < 0) return error.InvalidCount;
        try r.skip(@as(usize, @intCast(inlets)) * 8);
        const outlets = try r.readI32();
        if (outlets < 0) return error.InvalidCount;
        try r.skip(@as(usize, @intCast(outlets)) * 8);
    }
}

test "parses a minimal wiring file" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    var buf = try util.Buf(u8).init(a, 4096);
    var int_buf: [4]u8 = undefined;
    std.mem.writeInt(u32, &int_buf, util.MAGIC, .little);
    try buf.appendSlice(a, &int_buf);
    std.mem.writeInt(u32, &int_buf, util.FILE_VERSION, .little);
    try buf.appendSlice(a, &int_buf);
    for (0..util.HASH_SIZE) |_| try buf.append(a, 0x42);
    std.mem.writeInt(i32, &int_buf, 6, .little);
    try buf.appendSlice(a, &int_buf);
    const table_pos = buf.len;
    for (0..6) |_| try buf.appendSlice(a, &.{ 0, 0, 0, 0 });

    var starts: [6]u32 = undefined;
    starts[0] = @intCast(buf.len);
    std.mem.writeInt(i32, &int_buf, 1, .little);
    try buf.appendSlice(a, &int_buf);
    try buf.append(a, 1); // type
    std.mem.writeInt(i32, &int_buf, 0, .little);
    try buf.appendSlice(a, &int_buf); // id
    std.mem.writeInt(i32, &int_buf, 1, .little);
    try buf.appendSlice(a, &int_buf); // fanout count
    std.mem.writeInt(i32, &int_buf, 30, .little);
    try buf.appendSlice(a, &int_buf); // fanout
    starts[1] = @intCast(buf.len);
    std.mem.writeInt(i32, &int_buf, 0, .little);
    try buf.appendSlice(a, &int_buf);
    starts[2] = @intCast(buf.len);
    std.mem.writeInt(i32, &int_buf, 0, .little);
    try buf.appendSlice(a, &int_buf);
    starts[3] = @intCast(buf.len);
    std.mem.writeInt(i32, &int_buf, 0, .little);
    try buf.appendSlice(a, &int_buf);
    starts[4] = @intCast(buf.len);
    std.mem.writeInt(i32, &int_buf, 0, .little);
    try buf.appendSlice(a, &int_buf);
    starts[5] = @intCast(buf.len);

    for (starts, 0..) |s, i| {
        const off = table_pos + i * 4;
        std.mem.writeInt(u32, buf.items[off..][0..4], s, .little);
    }

    const parsed = try parseWiring(a, buf.slice());
    try std.testing.expectEqual(@as(usize, 1), parsed.input_ports.len);
    try std.testing.expectEqual(@as(i32, 0), parsed.input_ports[0].id);
    try std.testing.expectEqual(@as(usize, 1), parsed.input_ports[0].fanout.len);
    try std.testing.expectEqual(@as(i32, 30), parsed.input_ports[0].fanout[0]);
}

test "rejects bad magic" {
    const a = std.testing.allocator;
    const bad = [_]u8{ 0x00, 0x00, 0x00, 0x00 };
    try std.testing.expectError(error.InvalidMagic, parseWiring(a, &bad));
}
