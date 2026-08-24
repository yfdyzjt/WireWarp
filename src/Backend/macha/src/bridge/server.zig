const std = @import("std");
const util = @import("../util.zig");
const pipe = @import("pipe.zig");
const protocol = @import("protocol.zig");
const Session = @import("Session.zig");

pub const ServeError = error{ IoError, IdGap } || std.mem.Allocator.Error;

/// Connect once, serve until the connection ends, then exit with a message.
/// The backend never reconnects: the frontend is expected to (re)start the
/// backend process when it opens a new pipe.
pub fn run(io: std.Io, a: std.mem.Allocator, pipe_override: ?[]const u8, empty: bool) void {
    var backend = Session.init(a) catch @panic("out of memory");
    defer backend.deinit();

    var send_id: i64 = 0;
    var last_request_id: i64 = 0;

    const fd = pipe.connect(a, pipe_override) catch |err| {
        std.debug.print("macha: cannot connect to the frontend pipe ({s}); exiting\n", .{@errorName(err)});
        std.process.exit(1);
    };
    std.debug.print("frontend connected\n", .{});
    defer pipe.close(fd);

    serve(fd, io, &backend, &send_id, &last_request_id, empty) catch |err| {
        std.debug.print("macha: connection ended ({s}); exiting\n", .{@errorName(err)});
        std.process.exit(1);
    };
    std.debug.print("macha: frontend closed the connection; exiting\n", .{});
}

pub fn serve(
    fd: pipe.Handle,
    io: std.Io,
    backend: *Session,
    send_id: *i64,
    last_request_id: *i64,
    empty: bool,
) ServeError!void {
    var msg_buf = try std.ArrayList(u8).initCapacity(backend.alloc, 4096);
    defer msg_buf.deinit(backend.alloc);
    var ack_buf = try std.ArrayList(u8).initCapacity(backend.alloc, 4096);
    defer ack_buf.deinit(backend.alloc);

    while (true) {
        const msg = protocol.readMessage(fd, backend.alloc, &msg_buf) catch |err| switch (err) {
            error.CleanEof => return,
            else => return error.IoError,
        } orelse return; // clean EOF

        if (last_request_id.* != 0 and msg.id != last_request_id.* + 1) {
            return error.IdGap;
        }
        last_request_id.* = msg.id;

        if (empty) {
            try protocol.packAck(backend.alloc, &ack_buf, 0, "", &.{});
        } else {
            const ack = backend.dispatch(io, msg.tag, msg.body);
            if (ack.status != 0) {
                std.debug.print("{} failed: {s}\n", .{ msg.tag, ack.message });
            }
            try protocol.packAck(backend.alloc, &ack_buf, ack.status, ack.message, ack.payload);
        }
        send_id.* += 1;
        protocol.writeMessage(fd, protocol.ackTagOf(msg.tag), send_id.*, ack_buf.items) catch
            return error.IoError;
    }
}
