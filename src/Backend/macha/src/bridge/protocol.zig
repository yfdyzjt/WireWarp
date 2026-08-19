//! Wire format (all little-endian, mirroring the frontend `Transport.cs`):
//!
//! ```text
//! header: u32 magic (0xBADBEEF) | u16 version (1) | u16 tag | i64 id | i32 body length
//! body:   `length` bytes
//! ```
//!
//! Acknowledgment bodies are: `i32 status | string message | payload bytes`.

const std = @import("std");
const util = @import("../util.zig");
const pipe = @import("pipe.zig");

pub const magic: u32 = 0xBADBEEF;
pub const version: u16 = 1;

pub const header_len: usize = 20;
pub const max_body_len: usize = 256 * 1024 * 1024;

pub const Tag = enum(u16) {
    startup = 1,
    startup_ack = 2,

    sync_to = 3,
    sync_to_ack = 4,

    sync_from = 5,
    sync_from_ack = 6,

    reset = 7,
    reset_ack = 8,

    frame = 9,
    frame_ack = 10,

    shutdown = 11,
    shutdown_ack = 12,

    _,
};

/// A decoded pipe message. `body` points into the caller's reuse buffer and
/// is only valid until the next `readMessage`.
pub const Message = struct {
    tag: Tag,
    id: i64,
    body: []const u8,
};

pub const ReadError = error{
    CleanEof,
    Truncated,
    ReadFailed,
    BadHeader,
} || std.mem.Allocator.Error;

pub fn readMessage(fd: pipe.Handle, a: std.mem.Allocator, body: *util.Buf(u8)) ReadError!?Message {
    var header: [header_len]u8 = undefined;
    if (!try readExact(fd, &header)) return null;

    var hr = util.Reader.init(&header);
    const m = hr.readU32() catch return error.BadHeader;
    const v = hr.readU16() catch return error.BadHeader;
    const t: Tag = @enumFromInt(hr.readU16() catch return error.BadHeader);
    const id = hr.readI64() catch return error.BadHeader;
    const len = hr.readU32() catch return error.BadHeader;

    if (m != magic) return error.BadHeader;
    if (v != version) return error.BadHeader;
    if (len > max_body_len) return error.BadHeader;

    body.clear();
    try body.ensureUnusedCapacity(a, len);
    if (len > 0) {
        if (!try readExact(fd, body.items[0..len])) return error.Truncated;
        body.len = len;
    }
    return .{ .tag = t, .id = id, .body = body.slice() };
}

fn readExact(fd: pipe.Handle, buf: []u8) ReadError!bool {
    // Returns true if every byte was read, false on a clean EOF with zero
    // bytes consumed.
    var n: usize = 0;
    while (n < buf.len) {
        const k = pipe.read(fd, buf[n..]) catch return error.ReadFailed;
        if (k == 0) {
            if (n == 0) return false;
            return error.Truncated;
        }
        n += k;
    }
    return true;
}

pub const WriteError = error{WriteFailed};

pub fn writeMessage(fd: pipe.Handle, t: Tag, id: i64, body: []const u8) WriteError!void {
    var header: [header_len]u8 = undefined;
    std.mem.writeInt(u32, header[0..4], magic, .little);
    std.mem.writeInt(u16, header[4..6], version, .little);
    std.mem.writeInt(u16, header[6..8], @intFromEnum(t), .little);
    std.mem.writeInt(i64, header[8..16], id, .little);
    std.mem.writeInt(u32, header[16..20], @intCast(body.len), .little);
    try writeAll(fd, &header);
    try writeAll(fd, body);
}

fn writeAll(fd: pipe.Handle, bytes: []const u8) WriteError!void {
    var off: usize = 0;
    while (off < bytes.len) {
        const n = pipe.write(fd, bytes[off..]) catch return error.WriteFailed;
        if (n == 0) return error.WriteFailed;
        off += n;
    }
}

pub fn packAck(a: std.mem.Allocator, buf: *util.Buf(u8), status: i32, message: []const u8, payload: []const u8) std.mem.Allocator.Error!void {
    buf.clear();
    try util.writeIntLe(i32, a, buf, status);
    try util.writeString(a, buf, message);
    try buf.appendSlice(a, payload);
}

pub fn ackTagOf(t: Tag) Tag {
    return @enumFromInt(@intFromEnum(t) + 1);
}

const testing = std.testing;

fn pair(a: std.mem.Allocator) !struct { peer: std.posix.socket_t, ours: std.posix.socket_t, buf: *util.Buf(u8) } {
    var sv: [2]std.posix.socket_t = undefined;
    const rc = std.posix.system.socketpair(std.posix.AF.UNIX, std.posix.SOCK.STREAM, 0, &sv);
    if (std.posix.errno(rc) != .SUCCESS) return error.SocketPairFailed;
    const buf = try a.create(util.Buf(u8));
    buf.* = try util.Buf(u8).init(a, 64);
    return .{ .peer = sv[0], .ours = sv[1], .buf = buf };
}

test "message roundtrip over socketpair" {
    const a = std.testing.allocator;
    var p = try pair(a);
    defer a.destroy(p.buf);
    defer p.buf.deinit(a);
    defer util.closeFd(p.peer);
    defer util.closeFd(p.ours);

    const body = [_]u8{0xAA} ** 37;
    try writeMessage(p.peer, .frame, 7, &body);
    const msg = (try readMessage(p.ours, a, p.buf)).?;
    try testing.expectEqual(Tag.frame, msg.tag);
    try testing.expectEqual(@as(i64, 7), msg.id);
    try testing.expectEqualSlices(u8, &body, msg.body);
}

test "empty body roundtrip" {
    const a = std.testing.allocator;
    var p = try pair(a);
    defer a.destroy(p.buf);
    defer p.buf.deinit(a);
    defer util.closeFd(p.peer);
    defer util.closeFd(p.ours);

    try writeMessage(p.peer, .startup, 1, &.{});
    const msg = (try readMessage(p.ours, a, p.buf)).?;
    try testing.expectEqual(Tag.startup, msg.tag);
    try testing.expectEqual(@as(i64, 1), msg.id);
    try testing.expectEqual(@as(usize, 0), msg.body.len);
}

test "clean eof detected" {
    const a = std.testing.allocator;
    var p = try pair(a);
    defer a.destroy(p.buf);
    defer p.buf.deinit(a);

    util.closeFd(p.peer); // peer gone before any byte
    defer util.closeFd(p.ours);
    try testing.expectEqual(@as(?Message, null), try readMessage(p.ours, a, p.buf));
}

test "truncation detected" {
    const a = std.testing.allocator;
    var p = try pair(a);
    defer a.destroy(p.buf);
    defer p.buf.deinit(a);

    defer util.closeFd(p.ours);

    var header: [header_len]u8 = undefined;
    std.mem.writeInt(u32, header[0..4], magic, .little);
    std.mem.writeInt(u16, header[4..6], version, .little);
    std.mem.writeInt(u16, header[6..8], @intFromEnum(Tag.frame), .little);
    std.mem.writeInt(i64, header[8..16], 1, .little);
    std.mem.writeInt(u32, header[16..20], 100, .little);
    try writeAll(p.peer, &header);
    const ten = [_]u8{0} ** 10;
    try writeAll(p.peer, &ten);
    util.closeFd(p.peer);

    try testing.expectError(error.Truncated, readMessage(p.ours, a, p.buf));
}

test "bad magic rejected" {
    const a = std.testing.allocator;
    var p = try pair(a);
    defer a.destroy(p.buf);
    defer p.buf.deinit(a);

    defer util.closeFd(p.ours);

    var header: [header_len]u8 = undefined;
    std.mem.writeInt(u32, header[0..4], 0xDEADBEEF, .little);
    std.mem.writeInt(u16, header[4..6], version, .little);
    std.mem.writeInt(u16, header[6..8], @intFromEnum(Tag.frame), .little);
    std.mem.writeInt(i64, header[8..16], 1, .little);
    std.mem.writeInt(u32, header[16..20], 0, .little);
    try writeAll(p.peer, &header);
    util.closeFd(p.peer);

    try testing.expectError(error.BadHeader, readMessage(p.ours, a, p.buf));
}

test "ack pack layout" {
    const a = std.testing.allocator;
    var buf = try util.Buf(u8).init(a, 64);
    defer buf.deinit(a);
    try packAck(a, &buf, 0, "ok", &.{ 1, 2, 3 });
    var r = util.Reader.init(buf.slice());
    try testing.expectEqual(@as(i32, 0), try r.readI32());
    try testing.expectEqualStrings("ok", try r.readString());
    try testing.expectEqualSlices(u8, &.{ 1, 2, 3 }, try r.readBytes(3));
}
