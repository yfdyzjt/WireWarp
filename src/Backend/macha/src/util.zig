const std = @import("std");

pub const MAGIC: u32 = 0xBADBEEF;
pub const FILE_VERSION: u32 = 1;
pub const HASH_SIZE: usize = 32;

pub const Reader = struct {
    pub const Error = error{ UnexpectedEof, InvalidLength };

    buf: []const u8,
    pos: usize = 0,

    pub fn init(buf: []const u8) Reader {
        return .{ .buf = buf };
    }

    pub fn remaining(self: *const Reader) usize {
        return self.buf.len - self.pos;
    }

    pub fn position(self: *const Reader) usize {
        return self.pos;
    }

    pub fn seek(self: *Reader, pos: usize) Error!void {
        if (pos > self.buf.len) return error.UnexpectedEof;
        self.pos = pos;
    }

    pub fn readBytes(self: *Reader, n: usize) Error![]const u8 {
        if (self.remaining() < n) return error.UnexpectedEof;
        const out = self.buf[self.pos .. self.pos + n];
        self.pos += n;
        return out;
    }

    pub fn skip(self: *Reader, n: usize) Error!void {
        _ = try self.readBytes(n);
    }

    pub fn readU8(self: *Reader) Error!u8 {
        return (try self.readBytes(1))[0];
    }

    pub fn readU16(self: *Reader) Error!u16 {
        const b = try self.readBytes(2);
        return std.mem.readInt(u16, b[0..2], .little);
    }

    pub fn readU32(self: *Reader) Error!u32 {
        const b = try self.readBytes(4);
        return std.mem.readInt(u32, b[0..4], .little);
    }

    pub fn readI32(self: *Reader) Error!i32 {
        const b = try self.readBytes(4);
        return std.mem.readInt(i32, b[0..4], .little);
    }

    pub fn readI64(self: *Reader) Error!i64 {
        const b = try self.readBytes(8);
        return std.mem.readInt(i64, b[0..8], .little);
    }

    pub fn readU64(self: *Reader) Error!u64 {
        const b = try self.readBytes(8);
        return std.mem.readInt(u64, b[0..8], .little);
    }

    pub fn readString(self: *Reader) Error![]const u8 {
        // Mirrors C# BinaryReader.Read7BitEncodedInt: up to 4 full 7-bit
        // bytes, then a 5th byte limited to 0x0F; longer forms are invalid.
        var len: u32 = 0;
        var shift: u5 = 0;
        for (0..4) |_| {
            const b = try self.readU8();
            len |= @as(u32, b & 0x7F) << shift;
            if (b & 0x80 == 0) return self.readBytes(@intCast(len));
            shift += 7;
        }
        const last = try self.readU8();
        if (last > 0x0F) return error.InvalidLength;
        len |= @as(u32, last) << 28;
        return self.readBytes(@intCast(len));
    }
};

/// Growable buffer following the std.ArrayList convention: `items` is the
/// *used* slice (`items.len` is the logical length), `capacity` is the
/// allocation size and is always >= `items.len`.
pub fn Buf(comptime T: type) type {
    return struct {
        const Self = @This();

        items: []T = &.{},
        capacity: usize = 0,

        pub fn init(a: std.mem.Allocator, cap: usize) std.mem.Allocator.Error!Self {
            const alloc = try a.alloc(T, cap);
            return .{ .items = alloc[0..0], .capacity = alloc.len };
        }

        pub fn deinit(self: *Self, a: std.mem.Allocator) void {
            a.free(self.items.ptr[0..self.capacity]);
            self.items = &.{};
            self.capacity = 0;
        }

        pub fn ensureUnusedCapacity(self: *Self, a: std.mem.Allocator, additional: usize) std.mem.Allocator.Error!void {
            if (self.capacity - self.items.len >= additional) return;
            const used = self.items.len;
            const needed = used + additional;
            const doubled = self.capacity *| 2; // saturating
            const new_cap = @max(doubled, needed);
            const alloc = try a.realloc(self.items.ptr[0..self.capacity], new_cap);
            self.items = alloc[0..used];
            self.capacity = alloc.len;
        }

        pub fn append(self: *Self, a: std.mem.Allocator, v: T) std.mem.Allocator.Error!void {
            try self.ensureUnusedCapacity(a, 1);
            self.appendAssumeCapacity(v);
        }

        pub fn appendAssumeCapacity(self: *Self, v: T) void {
            self.items.ptr[self.items.len] = v;
            self.items.len += 1;
        }

        pub fn appendSlice(self: *Self, a: std.mem.Allocator, s: []const T) std.mem.Allocator.Error!void {
            try self.ensureUnusedCapacity(a, s.len);
            self.appendSliceAssumeCapacity(s);
        }

        pub fn appendSliceAssumeCapacity(self: *Self, s: []const T) void {
            @memcpy(self.items.ptr[self.items.len..][0..s.len], s);
            self.items.len += s.len;
        }

        pub fn slice(self: *const Self) []const T {
            return self.items;
        }

        pub fn clear(self: *Self) void {
            self.items.len = 0;
        }
    };
}

/// Close a socket fd (the 0.16 `std.posix` layer only exposes the raw
/// errno-returning syscall).
pub fn closeFd(fd: std.posix.socket_t) void {
    _ = std.posix.system.close(fd);
}

pub fn writeIntLe(comptime T: type, a: std.mem.Allocator, buf: *Buf(u8), v: T) std.mem.Allocator.Error!void {
    try buf.ensureUnusedCapacity(a, @sizeOf(T));
    var bytes: [@sizeOf(T)]u8 = undefined;
    std.mem.writeInt(T, &bytes, v, .little);
    buf.appendSliceAssumeCapacity(&bytes);
}

pub fn writeString(a: std.mem.Allocator, buf: *Buf(u8), s: []const u8) std.mem.Allocator.Error!void {
    var len = s.len;
    while (len >= 0x80) {
        try buf.append(a, @as(u8, @intCast(len & 0x7F)) | 0x80);
        len >>= 7;
    }
    try buf.append(a, @intCast(len));
    try buf.appendSlice(a, s);
}

test "reader little-endian and strings" {
    const a = std.testing.allocator;
    var buf = try Buf(u8).init(a, 64);
    defer buf.deinit(a);
    try buf.append(a, 0x78);
    try buf.append(a, 0x56);
    try buf.append(a, 0x34);
    try buf.append(a, 0x12);
    try buf.append(a, 3); // string length 3
    try buf.appendSlice(a, "abc");
    var r = Reader.init(buf.slice());
    try std.testing.expectEqual(@as(u32, 0x12345678), try r.readU32());
    try std.testing.expectEqualStrings("abc", try r.readString());
}

test "buf grows and keeps contents" {
    const a = std.testing.allocator;
    var buf = try Buf(i32).init(a, 2);
    defer buf.deinit(a);
    try buf.appendSlice(a, &.{ 1, 2, 3, 4, 5 });
    try std.testing.expectEqualSlices(i32, &.{ 1, 2, 3, 4, 5 }, buf.slice());
    buf.clear();
    try buf.append(a, 42);
    try std.testing.expectEqualSlices(i32, &.{42}, buf.slice());
}

test "writer helpers match BinaryWriter" {
    const a = std.testing.allocator;
    var buf = try Buf(u8).init(a, 64);
    defer buf.deinit(a);
    try writeIntLe(i32, a, &buf, -123456);
    try writeString(a, &buf, "ok");
    try writeString(a, &buf, "");
    try writeString(a, &buf, "x" ** 300);

    var r = Reader.init(buf.slice());
    try std.testing.expectEqual(@as(i32, -123456), try r.readI32());
    try std.testing.expectEqualStrings("ok", try r.readString());
    try std.testing.expectEqualStrings("", try r.readString());
    try std.testing.expectEqualStrings("x" ** 300, try r.readString());
}

test "7-bit lengths match C# Write7BitEncodedInt" {
    // Reference values from galvanic's reader tests (C# BinaryWriter).
    const cases = [_]struct { len: usize, bytes: []const u8 }{
        .{ .len = 0, .bytes = &.{0x00} },
        .{ .len = 1, .bytes = &.{0x01} },
        .{ .len = 0x7F, .bytes = &.{0x7F} },
        .{ .len = 0x80, .bytes = &.{ 0x80, 0x01 } },
        .{ .len = 0x3FFF, .bytes = &.{ 0xFF, 0x7F } },
        .{ .len = 0x4000, .bytes = &.{ 0x80, 0x80, 0x01 } },
        .{ .len = 1_000_000, .bytes = &.{ 0xC0, 0x84, 0x3D } },
    };
    const a = std.testing.allocator;
    for (cases) |c| {
        const payload = try a.alloc(u8, c.len);
        defer a.free(payload);
        @memset(payload, 'x');
        var buf = try Buf(u8).init(a, c.len + 8);
        defer buf.deinit(a);
        try writeString(a, &buf, payload);
        try std.testing.expectEqualSlices(u8, c.bytes, buf.items[0..c.bytes.len]);
        try std.testing.expectEqual(c.len + c.bytes.len, buf.items.len);
    }
}

test "7-bit length decode rejects overlong forms" {
    const a = std.testing.allocator;

    // Simple single-byte length still decodes.
    var buf = try Buf(u8).init(a, 256);
    defer buf.deinit(a);
    try buf.append(a, 0x7F);
    try buf.appendSlice(a, "x" ** 127);
    var r = Reader.init(buf.slice());
    try std.testing.expectEqual(@as(usize, 127), (try r.readString()).len);

    // 5-byte form with the maximal 4-bit tail is valid (then runs out of data).
    var b5 = try Buf(u8).init(a, 16);
    defer b5.deinit(a);
    try b5.appendSlice(a, &.{ 0x80, 0x80, 0x80, 0x80, 0x0F });
    var r5 = Reader.init(b5.slice());
    try std.testing.expectError(error.UnexpectedEof, r5.readString());

    // 5th byte above 0x0F is rejected (C# FormatException).
    var b6 = try Buf(u8).init(a, 16);
    defer b6.deinit(a);
    try b6.appendSlice(a, &.{ 0x80, 0x80, 0x80, 0x80, 0x10 });
    var r6 = Reader.init(b6.slice());
    try std.testing.expectError(error.InvalidLength, r6.readString());

    // 6-byte encodings are rejected outright.
    var b7 = try Buf(u8).init(a, 16);
    defer b7.deinit(a);
    try b7.appendSlice(a, &.{ 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 });
    var r7 = Reader.init(b7.slice());
    try std.testing.expectError(error.InvalidLength, r7.readString());
}
