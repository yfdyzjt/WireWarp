//! V4.1: immutable ordered gate-to-next-wave continuations. No cross-wave
//! execution, no sorting, no deduplication, and no cached mutable state.
const std = @import("std");
const Graph = @import("Graph.zig");
const Self = @This();
pub const wire_max: usize = (1 << 20) - 1;
const payload_mask: u64 = (@as(u64, 1) << 60) - 1;
pub const Plan = struct {
    raw: u64,
    pub inline fn count(p: Plan) u2 { return @intCast(p.raw >> 62); }
    pub inline fn wire(p: Plan, comptime i: usize) usize {
        return @intCast((p.raw >> (20 * i)) & @as(u64, wire_max));
    }
    pub inline fn gate(p: Plan) usize { return @intCast(p.raw & payload_mask); }
    pub fn make(gi: usize, wires: []const usize) Plan {
        // Graph indices are validated signed 32-bit file IDs. The caller
        // also checks the gate count before making any fallback descriptor.
        std.debug.assert(gi <= payload_mask);
        if (wires.len == 0 or wires.len > 3) return .{ .raw = @intCast(gi) };
        var raw = @as(u64, @intCast(wires.len)) << 62;
        for (wires, 0..) |w, i| {
            if (w > wire_max) return .{ .raw = @intCast(gi) };
            raw |= @as(u64, @intCast(w)) << @as(u6, @intCast(i * 20));
        }
        return .{ .raw = raw };
    }
};
plans: []Plan,
pending: std.ArrayList(Plan),
counts: [4]usize = [_]usize{0} ** 4,

pub fn init(a: std.mem.Allocator, g: *const Graph) std.mem.Allocator.Error!Self {
    if (g.gates.len > payload_mask) return error.OutOfMemory;
    const plans = try a.alloc(Plan, g.gates.len);
    errdefer a.free(plans);
    var self: Self = .{ .plans = plans, .pending = try std.ArrayList(Plan).initCapacity(a, g.gates.len) };
    for (g.gates, plans, 0..) |gate, *p, gi| {
        p.* = Plan.make(gi, gate.wires);
        self.counts[p.count()] += 1;
    }
    return self;
}

comptime { if (@sizeOf(Plan) != 8) @compileError("Continuation plan must be 8 bytes"); }

test "continuations preserve wire count, order, repeats and overflow fallback" {
    const t = std.testing;
    const p = Plan.make(42, &.{ wire_max, 0, wire_max });
    try t.expectEqual(@as(u2, 3), p.count());
    try t.expectEqual(wire_max, p.wire(0));
    try t.expectEqual(@as(usize, 0), p.wire(1));
    try t.expectEqual(wire_max, p.wire(2));
    const one = Plan.make(43, &.{0});
    try t.expectEqual(@as(u2, 1), one.count());
    try t.expectEqual(@as(usize, 0), one.wire(0));
    const two = Plan.make(44, &.{ 9, 8 });
    try t.expectEqual(@as(u2, 2), two.count());
    try t.expectEqual(@as(usize, 9), two.wire(0));
    try t.expectEqual(@as(usize, 8), two.wire(1));
    for ([_][]const usize{ &.{}, &.{ 1, 2, 3, 4 }, &.{ wire_max + 1 }, &.{ 1, wire_max + 1, 2 } }) |ws| {
        const f = Plan.make(45, ws);
        try t.expectEqual(@as(u2, 0), f.count());
        try t.expectEqual(@as(usize, 45), f.gate());
    }
}
