//! V4.0: compact immutable operands; live lamp/state reads, original scheduler.
const std = @import("std");
const Graph = @import("Graph.zig");
const PackedGates = @This();

pub const Op = enum(u3) { fallback, normal1, fault1, lazy_fault1, and2, xor2 };
pub const Plan = packed struct(u64) {
    first: u32 = 0,
    second: u29 = 0,
    op: Op = .fallback,
};
plans: []Plan,
counts: [6]usize = [_]usize{0} ** 6,

// Avoid pointer truncation; validate range/alignment before deriving an index.
fn lampIndex(g: *const Graph, lamp: *Graph.Lamp) ?u32 {
    const base = @intFromPtr(g.lamps.ptr);
    const ptr = @intFromPtr(lamp);
    if (ptr < base) return null;
    const offset = ptr - base;
    if (offset % @sizeOf(Graph.Lamp) != 0) return null;
    const index = offset / @sizeOf(Graph.Lamp);
    if (index >= g.lamps.len or index > std.math.maxInt(u32)) return null;
    return @intCast(index);
}

pub fn makePlan(g: *const Graph, gate: Graph.Gate, lazy_wire: ?u32) Plan {
    const op: Op = switch (gate.class) {
        .gate_1 => .normal1,
        .fault_1 => if (lazy_wire != null) .lazy_fault1 else .fault1,
        .gate_2_and => .and2,
        .gate_2_xor => .xor2,
        else => return .{},
    };
    const two = op == .and2 or op == .xor2;
    if (gate.lamps.len != (if (two) @as(usize, 2) else @as(usize, 1))) return .{};
    const first = lampIndex(g, gate.lamps[0]) orelse return .{};
    const second: u32 = if (two) (lampIndex(g, gate.lamps[1]) orelse return .{}) else (lazy_wire orelse 0);
    if (second > std.math.maxInt(u29)) return .{};
    return .{ .first = first, .second = @intCast(second), .op = op };
}

// A null result delegates to the existing evaluator, including fault_n RNG.
// comptime lazy mirrors the existing Regions.evaluate policy, not a new rule.
pub inline fn evaluate(self: *const PackedGates, sim: anytype, gi: usize, lazy_state: []const u8, comptime lazy: bool) ?bool {
    const p = self.plans[gi];
    if (p.op == .fallback) return null;
    const a = sim.g.lamps[p.first].on;
    const cur = switch (p.op) {
        .fallback => unreachable,
        .fault1 => return a,
        .lazy_fault1 => return if (lazy) a != (lazy_state[p.second] & 1 != 0) else a,
        .normal1 => a,
        .and2 => a and sim.g.lamps[p.second].on,
        .xor2 => a != sim.g.lamps[p.second].on,
    };
    const changed = cur != sim.gate_state[gi];
    sim.gate_state[gi] = cur;
    return changed;
}

comptime { if (@sizeOf(Plan) != 8) @compileError("Packed gate plan must be 8 bytes"); }


test "packed gate truth table, lazy parity and fallback" {
    const t = std.testing;
    var lamps = [_]Graph.Lamp{
        .{ .gate = 0, .on = false, .initial = false },
        .{ .gate = 0, .on = false, .initial = false },
    };
    var refs = [_]*Graph.Lamp{ &lamps[0], &lamps[1] };
    const g: Graph = .{ .lamps = &lamps, .gates = &.{}, .wires = &.{}, .input_ports = &.{} };
    var state = [_]bool{false};
    const FakeSim = struct { g: *const Graph, gate_state: []bool };
    var sim: FakeSim = .{ .g = &g, .gate_state = &state };
    var plans = [_]Plan{.{}};
    const table: PackedGates = .{ .plans = &plans };
    const classes = [_]Graph.Gate.Class{ .gate_1, .fault_1, .gate_2_and, .gate_2_xor, .fault_n, .gate_n_and, .gate_n_xor };
    for (classes) |class| {
        const n: usize = if (class == .gate_1 or class == .fault_1) 1 else 2;
        const gate: Graph.Gate = .{ .class = class, .lamps = refs[0..n], .wires = &.{}, .state = false };
        for (0..2) |use_lazy| {
            const wi: ?u32 = if (use_lazy == 1 and class == .fault_1) 1 else null;
            plans[0] = makePlan(&g, gate, wi);
            for (0..4) |bits| {
                lamps[0].on = bits & 1 != 0;
                lamps[1].on = bits & 2 != 0;
                for (0..2) |prior| {
                    for (0..4) |parity| {
                        state[0] = prior != 0;
                        const old = state[0];
                        const lazy_state = [_]u8{ 0, @intCast(parity) };
                        const got = table.evaluate(&sim, 0, &lazy_state, true);
                        switch (class) {
                            .fault_n, .gate_n_and, .gate_n_xor => {
                                try t.expectEqual(@as(?bool, null), got);
                                try t.expectEqual(old, state[0]);
                            },
                            .fault_1 => {
                                try t.expectEqual(@as(?bool, lamps[0].on != (wi != null and parity & 1 != 0)), got);
                                try t.expectEqual(old, state[0]);
                                try t.expectEqual(@as(?bool, lamps[0].on), table.evaluate(&sim, 0, &lazy_state, false));
                            },
                            else => {
                                const cur = switch (class) {
                                    .gate_1 => lamps[0].on,
                                    .gate_2_and => lamps[0].on and lamps[1].on,
                                    .gate_2_xor => lamps[0].on != lamps[1].on,
                                    else => unreachable,
                                };
                                try t.expectEqual(@as(?bool, cur != old), got);
                                try t.expectEqual(cur, state[0]);
                            },
                        }
                    }
                }
            }
        }
    }
    const fault: Graph.Gate = .{ .class = .fault_1, .lamps = refs[0..1], .wires = &.{}, .state = false };
    try t.expectEqual(Op.lazy_fault1, makePlan(&g, fault, std.math.maxInt(u29)).op);
    try t.expectEqual(Op.fallback, makePlan(&g, fault, @as(u32, std.math.maxInt(u29)) + 1).op);
    var outside = Graph.Lamp{ .gate = 0, .on = false, .initial = false };
    refs[0] = &outside;
    try t.expectEqual(Op.fallback, makePlan(&g, fault, null).op);
}
