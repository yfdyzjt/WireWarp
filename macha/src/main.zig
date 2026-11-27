const std = @import("std");
const macha = @import("macha");

const usage = "usage: macha serve [--empty] [--pipe <path>]\n";

pub fn main(init: std.process.Init) void {
    const arena = init.arena.allocator();
    const args = init.minimal.args.toSlice(arena) catch return;
    if (args.len < 2 or !std.mem.eql(u8, args[1], "serve")) {
        std.debug.print("{s}", .{usage});
        return;
    }

    var override: ?[]const u8 = null;
    var empty = false;
    var i: usize = 2;
    while (i < args.len) : (i += 1) {
        if (std.mem.eql(u8, args[i], "--pipe")) {
            i += 1;
            if (i >= args.len) {
                std.debug.print("macha: --pipe requires a path\n", .{});
                return;
            }
            override = args[i];
        } else if (std.mem.eql(u8, args[i], "--empty")) {
            empty = true;
        } else if (std.mem.eql(u8, args[i], "-h") or std.mem.eql(u8, args[i], "--help")) {
            std.debug.print("{s}", .{usage});
            return;
        } else {
            std.debug.print("macha: unknown serve argument {s}\n", .{args[i]});
            return;
        }
    }
    macha.server.run(init.io, std.heap.page_allocator, override, empty);
}
