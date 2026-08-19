//! Compiles parsed world files into the runtime `Graph`.
//!
//! Id layout: the wiring file shares one id space across its groups — input
//! ports `[0, n_in)`, output ports `[n_in, lamp_base)`, lamps
//! `[lamp_base, gate_base)`, gates `[gate_base, wire_base)`, wires
//! `[wire_base, end)`. Every group is serialized in ascending id order, so
//! record `i` of a group always has id `base + i`; the reader relies on both
//! invariants.

const std = @import("std");
const util = @import("../util.zig");
const files = @import("../bridge/files.zig");
const Graph = @import("Graph.zig");

pub const Error = error{
    InvalidLampType,
    InvalidGateType,
    InvalidWireType,
    InvalidOutputPortId,
    InvalidLampId,
    InvalidGateId,
    InvalidWireId,
    InvalidInputPortId,
    LampWithMultipleGates,
    LampWithoutGate,
    MissingFaultLamp,
    FaultGateWithoutLamp,
    InvalidLampGateId,
    InvalidWireTargetId,
    InvalidGateWireId,
    InvalidInputPortWireId,
} || std.mem.Allocator.Error;

const LampKind = enum(u8) {
    on = 1,
    off = 2,
    fault = 3,
};

const GateKind = enum(u8) {
    and_ = 1,
    nand = 2,
    or_ = 3,
    nor = 4,
    xor = 5,
    xnor = 6,
    fault = 7,
};

const WireColor = enum(u8) {
    red = 1,
    blue = 2,
    green = 3,
    yellow = 4,
};

/// Per-component tables built while validating file ids and types.
const Metadata = struct {
    lamp_kind: []LampKind,
    lamp_gate: []u32,
    lamp_initial: []bool,
    gate_kind: []GateKind,
};

/// Lamps bucketed by gate (compressed sparse row): gate `g` owns the run
/// `lamps[start[g]..][0..len[g]]`.
const LampColumns = struct {
    len: []usize,
    start: []usize,
    lamps: []usize,
};

/// `a` owns the returned graph; this function's scratch arena owns only
/// compile-time tables and is released before returning.
pub fn compile(a: std.mem.Allocator, wiring: *const files.WiringFile) Error!Graph {
    var scratch_arena = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    defer scratch_arena.deinit();
    const t = scratch_arena.allocator();

    const lamp_base: i32 = @intCast(wiring.input_ports.len + wiring.output_ports.len);
    const gate_base = lamp_base + @as(i32, @intCast(wiring.lamps.len));
    const wire_base = gate_base + @as(i32, @intCast(wiring.gates.len));

    const metadata = try readMetadata(t, wiring, lamp_base, gate_base, wire_base);
    const columns = try bucketLamps(t, metadata.lamp_gate, wiring.gates.len);

    const gates = try a.alloc(Graph.Gate, wiring.gates.len);
    const lamps = try a.alloc(Graph.Lamp, wiring.lamps.len);
    try buildGatesAndLamps(a, t, gates, lamps, metadata, columns);

    const wires = try a.alloc(Graph.Wire, wiring.wires.len);
    for (wires) |*w| w.targets = &.{};
    try buildWireTargets(a, wiring, lamp_base, gate_base, wire_base, metadata, gates, wires);
    try buildGateFanout(a, wiring, wire_base, gates);
    const input_ports = try buildInputPorts(a, wiring, wire_base);

    return .{
        .wires = wires,
        .lamps = lamps,
        .gates = gates,
        .input_ports = input_ports,
    };
}

fn readMetadata(t: std.mem.Allocator, wiring: *const files.WiringFile, lamp_base: i32, gate_base: i32, wire_base: i32) Error!Metadata {
    for (wiring.output_ports, 0..) |rec, i| {
        const id = @as(i32, @intCast(wiring.input_ports.len + i));
        if (rec.id != id) return error.InvalidOutputPortId;
    }

    const lamp_kind = try t.alloc(LampKind, wiring.lamps.len);
    const lamp_gate = try t.alloc(u32, wiring.lamps.len);
    const lamp_initial = try t.alloc(bool, wiring.lamps.len);
    for (wiring.lamps, 0..) |rec, i| {
        if (rec.id != lamp_base + @as(i32, @intCast(i))) return error.InvalidLampId;
        lamp_kind[i] = std.enums.fromInt(LampKind, rec.type_) orelse return error.InvalidLampType;
        lamp_initial[i] = lamp_kind[i] == .on;

        if (rec.fanout.len == 0) return error.LampWithoutGate;
        if (rec.fanout.len > 1) return error.LampWithMultipleGates;
        const gi = rec.fanout[0] - gate_base;
        if (gi < 0 or gi >= @as(i32, @intCast(wiring.gates.len))) return error.InvalidLampGateId;
        lamp_gate[i] = @intCast(gi);
    }

    const gate_kind = try t.alloc(GateKind, wiring.gates.len);
    for (wiring.gates, 0..) |rec, i| {
        if (rec.id != gate_base + @as(i32, @intCast(i))) return error.InvalidGateId;
        gate_kind[i] = std.enums.fromInt(GateKind, rec.type_) orelse return error.InvalidGateType;
    }

    for (wiring.wires, 0..) |rec, i| {
        if (rec.id != wire_base + @as(i32, @intCast(i))) return error.InvalidWireId;
        if (std.enums.fromInt(WireColor, rec.type_) == null) return error.InvalidWireType;
    }

    return .{
        .lamp_kind = lamp_kind,
        .lamp_gate = lamp_gate,
        .lamp_initial = lamp_initial,
        .gate_kind = gate_kind,
    };
}

fn bucketLamps(t: std.mem.Allocator, lamp_gate: []const u32, gate_count: usize) std.mem.Allocator.Error!LampColumns {
    const len = try t.alloc(usize, gate_count);
    @memset(len, 0);
    for (lamp_gate) |gi| len[gi] += 1;

    const start = try t.alloc(usize, gate_count);
    var total: usize = 0;
    for (len, 0..) |count, gi| {
        start[gi] = total;
        total += count;
    }

    const lamps = try t.alloc(usize, total);
    const cursor = try t.dupe(usize, start);
    for (lamp_gate, 0..) |gi, li| {
        lamps[cursor[gi]] = li;
        cursor[gi] += 1;
    }

    return .{ .len = len, .start = start, .lamps = lamps };
}

fn buildGatesAndLamps(
    a: std.mem.Allocator,
    t: std.mem.Allocator,
    gates: []Graph.Gate,
    lamps: []Graph.Lamp,
    metadata: Metadata,
    columns: LampColumns,
) Error!void {
    for (gates, 0..) |*g, gi| {
        const column = columns.lamps[columns.start[gi]..][0..columns.len[gi]];

        var params_len: usize = 0;
        var has_fault_lamp = false;
        for (column) |li| {
            if (metadata.lamp_kind[li] == .fault) has_fault_lamp = true else params_len += 1;
        }
        const params = try t.alloc(usize, params_len);
        var k: usize = 0;
        for (column) |li| {
            if (metadata.lamp_kind[li] != .fault) {
                params[k] = li;
                k += 1;
            }
        }

        g.lamps = try lampPtrs(a, lamps, params);
        g.wires = &.{};
        g.state = false;

        if (has_fault_lamp) {
            // A fault gate with no parameter lamp would divide by zero in the
            // sim's compound roll; such a gate can never fire, so reject it.
            if (params_len == 0) return error.FaultGateWithoutLamp;
            g.class = if (params_len == 1) .fault_1 else .fault_n;
        } else if (metadata.gate_kind[gi] == .fault) {
            return error.MissingFaultLamp;
        } else {
            const kind = metadata.gate_kind[gi];
            g.class = switch (kind) {
                .and_, .nand, .or_, .nor => andClass(params_len),
                .xor, .xnor => xorClass(params_len),
                .fault => unreachable,
            };
            if (kind == .or_ or kind == .nor) {
                // OR/NOR compile as AND/NAND over inverted inputs.
                for (params) |li| metadata.lamp_initial[li] = !metadata.lamp_initial[li];
            }
            g.state = initialState(g.class, params, metadata.lamp_initial);
        }
    }

    for (lamps, 0..) |*lamp, li| {
        lamp.gate = metadata.lamp_gate[li];
        lamp.on = metadata.lamp_initial[li];
        lamp.initial = metadata.lamp_initial[li];
    }
}

fn buildWireTargets(
    a: std.mem.Allocator,
    wiring: *const files.WiringFile,
    lamp_base: i32,
    gate_base: i32,
    wire_base: i32,
    metadata: Metadata,
    gates: []const Graph.Gate,
    wires: []Graph.Wire,
) Error!void {
    for (wiring.wires) |rec| {
        const wi: usize = @intCast(rec.id - wire_base);
        var count: usize = 0;
        for (rec.fanout) |id| {
            if (id >= lamp_base and id < gate_base) {
                count += 1;
            } else if (id >= @as(i32, @intCast(wiring.input_ports.len)) and id < lamp_base) {
                count += 1;
            } else {
                return error.InvalidWireTargetId;
            }
        }

        const targets = try a.alloc(Graph.Target, count);
        for (rec.fanout, 0..) |id, i| {
            if (id >= lamp_base and id < gate_base) {
                const li: usize = @intCast(id - lamp_base);
                // Fault lamp → fault-gate hit; plain lamp on a fault gate →
                // toggle only; any other lamp → toggle and dirty its gate.
                targets[i] = if (metadata.lamp_kind[li] == .fault)
                    .{ .fault_gate = metadata.lamp_gate[li] }
                else if (switch (gates[metadata.lamp_gate[li]].class) {
                    .fault_1, .fault_n => true,
                    else => false,
                })
                    .{ .lamp_on_fault = li }
                else
                    .{ .lamp = li };
            } else {
                targets[i] = .{ .output_port = id - @as(i32, @intCast(wiring.input_ports.len)) };
            }
        }
        std.mem.sortUnstable(Graph.Target, targets, {}, lessTarget);
        wires[wi].targets = targets;
    }
}

fn buildGateFanout(a: std.mem.Allocator, wiring: *const files.WiringFile, wire_base: i32, gates: []Graph.Gate) Error!void {
    for (wiring.gates, 0..) |rec, gi| {
        const wires = try a.alloc(usize, rec.fanout.len);
        for (rec.fanout, 0..) |id, i| {
            const wi = id - wire_base;
            if (wi < 0 or wi >= @as(i32, @intCast(wiring.wires.len))) return error.InvalidGateWireId;
            wires[i] = @intCast(wi);
        }
        std.mem.sortUnstable(usize, wires, {}, lessUsize);
        gates[gi].wires = wires;
    }
}

fn buildInputPorts(a: std.mem.Allocator, wiring: *const files.WiringFile, wire_base: i32) Error![]Graph.InputPort {
    const input_ports = try a.alloc(Graph.InputPort, wiring.input_ports.len);
    for (wiring.input_ports, 0..) |rec, i| {
        if (rec.id != @as(i32, @intCast(i))) return error.InvalidInputPortId;
        const wires = try a.alloc(usize, rec.fanout.len);
        for (rec.fanout, 0..) |id, j| {
            const wi = id - wire_base;
            if (wi < 0 or wi >= @as(i32, @intCast(wiring.wires.len))) return error.InvalidInputPortWireId;
            wires[j] = @intCast(wi);
        }
        std.mem.sortUnstable(usize, wires, {}, lessUsize);
        input_ports[i] = .{ .wires = wires };
    }
    return input_ports;
}

fn lessTarget(_: void, a: Graph.Target, b: Graph.Target) bool {
    return switch (a) {
        .lamp => |la| switch (b) {
            .lamp => |lb| la < lb,
            .lamp_on_fault, .fault_gate, .output_port => true, // lamps trip first
        },
        .lamp_on_fault => |la| switch (b) {
            .lamp => false,
            .lamp_on_fault => |lb| la < lb,
            .fault_gate, .output_port => true,
        },
        .fault_gate => |ga| switch (b) {
            .lamp, .lamp_on_fault => false,
            .fault_gate => |gb| ga < gb,
            .output_port => true,
        },
        .output_port => |oa| switch (b) {
            .lamp, .lamp_on_fault, .fault_gate => false,
            .output_port => |ob| oa < ob,
        },
    };
}

fn lessUsize(_: void, a: usize, b: usize) bool {
    return a < b;
}

fn andClass(n: usize) Graph.Gate.Class {
    return switch (n) {
        1 => .gate_1,
        2 => .gate_2_and,
        else => .gate_n_and,
    };
}

fn xorClass(n: usize) Graph.Gate.Class {
    return switch (n) {
        1 => .gate_1,
        2 => .gate_2_xor,
        else => .gate_n_xor,
    };
}

fn initialState(class: Graph.Gate.Class, params: []const usize, lamps: []const bool) bool {
    return switch (class) {
        .gate_1 => lamps[params[0]],
        .gate_2_and => lamps[params[0]] and lamps[params[1]],
        .gate_2_xor => lamps[params[0]] != lamps[params[1]],
        .gate_n_and, .gate_n_xor => {
            var on: u32 = 0;
            for (params) |li| {
                if (lamps[li]) on += 1;
            }
            return if (class == .gate_n_and) on == @as(u32, @intCast(params.len)) else on == 1;
        },
        .fault_1, .fault_n => false,
    };
}

fn lampPtrs(a: std.mem.Allocator, lamps: []Graph.Lamp, indices: []const usize) std.mem.Allocator.Error![]*Graph.Lamp {
    const refs = try a.alloc(*Graph.Lamp, indices.len);
    for (indices, 0..) |li, k| refs[k] = &lamps[li];
    return refs;
}
