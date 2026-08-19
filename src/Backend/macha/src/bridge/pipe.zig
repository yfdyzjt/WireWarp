//! Bridge transport: the client side of the WireWarp pipe.
//!
//! The frontend (running inside the game) creates a `NamedPipeServerStream`
//! named `WireWarp` and waits for a client. .NET implements named pipes on
//! Unix as domain sockets under `/tmp/CoreFxPipe_<name>`, and on Windows as
//! `\\.\pipe\<name>`.
//!
//! This backend is the connecting client: it retries until the frontend
//! creates the pipe, so the backend may be started before the game loads a
//! world.
//!
//! Override the location with `--pipe` (a Unix socket path, or a
//! `\\.\pipe\...` name on Windows).

const std = @import("std");
const builtin = @import("builtin");
const util = @import("../util.zig");

pub const pipe_name = "WireWarp";
pub const Handle = if (builtin.os.tag == .windows) std.os.windows.HANDLE else std.posix.socket_t;

pub const ConnectError = error{
    ConnectFailed,
    SocketFailed,
    NoCandidatePaths,
} || std.mem.Allocator.Error;

/// Connect to the first candidate location that answers.
pub fn connect(alloc: std.mem.Allocator, override_path: ?[]const u8) ConnectError!Handle {
    const paths = try candidatePaths(alloc, override_path);
    defer {
        for (paths) |p| alloc.free(p);
        alloc.free(paths);
    }
    if (paths.len == 0) return error.NoCandidatePaths;

    var last_error = error.ConnectFailed;
    for (paths) |path| {
        const result = if (builtin.os.tag == .windows) connectWindows(alloc, path) else connectUnix(path);
        if (result) |fd| {
            return fd;
        } else |_| {
            last_error = error.ConnectFailed;
        }
    }
    return last_error;
}

pub fn read(handle: Handle, bytes: []u8) error{ReadFailed}!usize {
    if (builtin.os.tag == .windows) return readWindows(handle, bytes);
    return std.posix.read(handle, bytes) catch error.ReadFailed;
}

pub fn write(handle: Handle, bytes: []const u8) error{WriteFailed}!usize {
    if (builtin.os.tag == .windows) return writeWindows(handle, bytes);
    const rc = std.posix.system.write(handle, bytes.ptr, bytes.len);
    return switch (std.posix.errno(rc)) {
        .SUCCESS => @intCast(rc),
        .INTR => write(handle, bytes),
        else => error.WriteFailed,
    };
}

pub fn close(handle: Handle) void {
    if (builtin.os.tag == .windows) {
        _ = CloseHandle(handle);
        return;
    }
    util.closeFd(handle);
}

extern "kernel32" fn CreateFileW([*:0]const u16, u32, u32, ?*anyopaque, u32, u32, ?std.os.windows.HANDLE) callconv(.winapi) std.os.windows.HANDLE;
extern "kernel32" fn ReadFile(std.os.windows.HANDLE, ?*anyopaque, u32, *u32, ?*anyopaque) callconv(.winapi) std.os.windows.BOOL;
extern "kernel32" fn WriteFile(std.os.windows.HANDLE, ?*const anyopaque, u32, *u32, ?*anyopaque) callconv(.winapi) std.os.windows.BOOL;
extern "kernel32" fn CloseHandle(std.os.windows.HANDLE) callconv(.winapi) std.os.windows.BOOL;

fn connectWindows(a: std.mem.Allocator, path: []const u8) error{ConnectFailed}!Handle {
    var wide = a.alloc(u16, path.len + 1) catch return error.ConnectFailed;
    defer a.free(wide);
    const len = std.unicode.utf8ToUtf16Le(wide[0..path.len], path) catch return error.ConnectFailed;
    wide[len] = 0;
    const handle = CreateFileW(wide[0..len :0].ptr, 0x80000000 | 0x40000000, 0, null, 3, 0, null);
    if (handle == std.os.windows.INVALID_HANDLE_VALUE) return error.ConnectFailed;
    return handle;
}

fn readWindows(handle: Handle, bytes: []u8) error{ReadFailed}!usize {
    var n: u32 = 0;
    if (!ReadFile(handle, bytes.ptr, @intCast(bytes.len), &n, null).toBool()) return error.ReadFailed;
    return n;
}

fn writeWindows(handle: Handle, bytes: []const u8) error{WriteFailed}!usize {
    var n: u32 = 0;
    if (!WriteFile(handle, bytes.ptr, @intCast(bytes.len), &n, null).toBool()) return error.WriteFailed;
    return n;
}

fn connectUnix(path: []const u8) error{ SocketFailed, ConnectFailed }!std.posix.socket_t {
    if (builtin.os.tag == .windows) return error.ConnectFailed;
    const fd = try socketUnix();
    errdefer util.closeFd(fd);

    var addr: std.posix.sockaddr.un = undefined;
    addr.family = std.posix.AF.UNIX;
    const path_len = @min(path.len, addr.path.len - 1);
    @memcpy(addr.path[0..path_len], path[0..path_len]);
    @memset(addr.path[path_len..], 0);
    const addr_len: std.posix.socklen_t = @intCast(@offsetOf(std.posix.sockaddr.un, "path") + path_len + 1);

    const rc = std.posix.system.connect(fd, @ptrCast(&addr), addr_len);
    switch (std.posix.errno(rc)) {
        .SUCCESS => return fd,
        else => return error.ConnectFailed,
    }
}

fn socketUnix() error{SocketFailed}!std.posix.socket_t {
    const rc = std.posix.system.socket(std.posix.AF.UNIX, std.posix.SOCK.STREAM, 0);
    return switch (std.posix.errno(rc)) {
        .SUCCESS => @intCast(rc),
        else => error.SocketFailed,
    };
}

/// Candidate pipe locations, in priority order. An explicit `--pipe`
/// override takes precedence over the .NET defaults.
pub fn candidatePaths(alloc: std.mem.Allocator, override_path: ?[]const u8) std.mem.Allocator.Error![][]u8 {
    if (override_path) |one| {
        if (one.len > 0) {
            const duped = try alloc.dupe(u8, one);
            return alloc.dupe([]u8, &.{duped});
        }
    }

    if (builtin.os.tag == .windows) {
        const path = try std.fmt.allocPrint(alloc, "\\\\.\\pipe\\{s}", .{pipe_name});
        return alloc.dupe([]u8, &.{path});
    }

    const out = try alloc.alloc([]u8, 2);
    out[0] = try std.fmt.allocPrint(alloc, "/tmp/CoreFxPipe_{s}", .{pipe_name});
    out[1] = try std.fmt.allocPrint(alloc, "/var/tmp/CoreFxPipe_{s}", .{pipe_name});
    return out;
}

test "override wins over defaults" {
    const a = std.testing.allocator;
    const paths = try candidatePaths(a, "/custom/pipe");
    defer {
        for (paths) |p| a.free(p);
        a.free(paths);
    }
    try std.testing.expectEqual(@as(usize, 1), paths.len);
    try std.testing.expectEqualStrings("/custom/pipe", paths[0]);
}

test "defaults include tmp" {
    if (builtin.os.tag == .windows) return; // no defaults yet on Windows
    const a = std.testing.allocator;
    const paths = try candidatePaths(a, null);
    defer {
        for (paths) |p| a.free(p);
        a.free(paths);
    }
    var found = false;
    for (paths) |p| {
        if (std.mem.endsWith(u8, p, "/CoreFxPipe_WireWarp")) found = true;
    }
    try std.testing.expect(found);
    try std.testing.expect(paths.len >= 2);
}
