const Sim = @This();

const std = @import("std");
const Buf = @import("../util.zig").Buf;
const Set = @import("../util.zig").Set;
const Graph = @import("Graph.zig");

alloc: std.mem.Allocator,
g: *const Graph,
rng: std.Random,
gate_state: []bool,
dirty: Set,
fault_hits: []u32,
fired: Set,

target_groups: Buf([]const usize),
out: Buf(i32),

events: u64 = 0,
checks: u64 = 0,

pub fn init(a: std.mem.Allocator, g: *const Graph, seed: u64) std.mem.Allocator.Error!Sim {
    const gate_count = g.gates.len;

    const lamps = g.lamps;
    for (lamps) |*l| l.on = l.initial;

    const gate_state = try a.alloc(bool, gate_count);
    for (g.gates, 0..) |gate, i| gate_state[i] = gate.state;

    const dirty_flag = try a.alloc(bool, gate_count);
    @memset(dirty_flag, false);

    const dirty_gates = try a.alloc(usize, gate_count);

    const fault_hits = try a.alloc(u32, gate_count);
    @memset(fault_hits, 0);

    const fired_flag = try a.alloc(bool, gate_count);
    @memset(fired_flag, false);

    const fired_list = try a.alloc(usize, gate_count);

    const prng = try a.create(std.Random.DefaultPrng);
    prng.* = std.Random.DefaultPrng.init(seed);

    return .{
        .alloc = a,
        .g = g,
        .rng = prng.random(),
        .gate_state = gate_state,
        .dirty = .{ .flags = dirty_flag, .items = dirty_gates },
        .fault_hits = fault_hits,
        .fired = .{ .flags = fired_flag, .items = fired_list },
        .target_groups = try Buf([]const usize).init(a, @max(gate_count, 1)),
        .out = try Buf(i32).init(a, 64),
    };
}

pub fn reset(self: *Sim) void {
    for (self.g.lamps) |*l| l.on = l.initial;
    for (self.g.gates, 0..) |gate, i| self.gate_state[i] = gate.state;
    self.dirty.reset();
    @memset(self.fault_hits, 0);
    self.clearEvent();
}

/// Applies one input event and returns the output ports it produced.
/// The returned slice is only valid until the next `event` call; the caller
/// must copy it with its own allocator if it needs to retain it.
pub fn event(self: *Sim, port: i32) std.mem.Allocator.Error![]const i32 {
    self.events += 1;
    self.clearEvent();
    std.debug.assert(self.dirty.len == 0);
    if (port >= 0 and port < @as(i32, @intCast(self.g.input_ports.len))) {
        self.target_groups.appendAssumeCapacity(self.g.input_ports[@intCast(port)].wires);
    }

    while (self.target_groups.items.len > 0) {
        for (self.target_groups.slice()) |wires| {
            for (wires) |w| {
                for (self.g.wires[w].targets) |t| {
                    switch (t) {
                        .lamp => |li| {
                            const cell = &self.g.lamps[li];
                            cell.on = !cell.on;
                            self.dirty.push(cell.gate);
                        },
                        .lamp_on_fault => |li| {
                            const cell = &self.g.lamps[li];
                            cell.on = !cell.on;
                        },
                        .fault_gate => |gi| {
                            if (self.g.gates[gi].class == .fault_n) self.fault_hits[gi] += 1;
                            self.dirty.push(gi);
                        },
                        .output_port => |p| try self.out.append(self.alloc, p),
                    }
                }
            }
        }
        self.target_groups.clear();

        for (self.dirty.slice()) |gi| {
            self.dirty.flags[gi] = false;
            self.checks += 1;
            if (self.evaluate(gi) and !self.fired.contains(gi)) {
                self.fired.push(gi);
                self.target_groups.appendAssumeCapacity(self.g.gates[gi].wires);
            }
        }
        self.dirty.clear();
    }

    return self.out.slice();
}

fn evaluate(self: *Sim, gi: usize) bool {
    const gate = self.g.gates[gi]; // value copy: lamp slices are shared
    switch (gate.class) {
        .fault_1 => {
            std.debug.assert(gate.lamps.len == 1);
            return gate.lamps[0].on;
        },
        .fault_n => {
            std.debug.assert(gate.lamps.len > 0); // compiler rejects 0-param fault gates
            const hits = self.fault_hits[gi];
            self.fault_hits[gi] = 0;
            if (hits == 0) return false;
            const lamp_count: u32 = @intCast(gate.lamps.len);
            var on_count: u32 = 0;
            for (gate.lamps) |l| {
                if (l.on) on_count += 1;
            }
            for (0..hits) |_| {
                if (self.rng.uintLessThan(u32, lamp_count) < on_count) return true;
            }
            return false;
        },
        .gate_1 => {
            return self.checkChanged(gi, gate.lamps[0].on);
        },
        .gate_2_and => {
            return self.checkChanged(gi, gate.lamps[0].on and gate.lamps[1].on);
        },
        .gate_2_xor => {
            return self.checkChanged(gi, gate.lamps[0].on != gate.lamps[1].on);
        },
        .gate_n_and => {
            var on: u32 = 0;
            for (gate.lamps) |l| {
                if (l.on) on += 1;
            }
            return self.checkChanged(gi, on == @as(u32, @intCast(gate.lamps.len)));
        },
        .gate_n_xor => {
            var on: u32 = 0;
            for (gate.lamps) |l| {
                if (l.on) on += 1;
            }
            return self.checkChanged(gi, on == 1);
        },
    }
}

fn checkChanged(self: *Sim, gi: usize, cur: bool) bool {
    const changed = cur != self.gate_state[gi];
    self.gate_state[gi] = cur;
    return changed;
}

fn clearEvent(self: *Sim) void {
    self.out.clear();
    self.target_groups.clear();
    // Reset only the flags this event actually set: a full-array memset
    // costs O(gate_count) cache-polluting writes per event.
    for (self.fired.slice()) |gi| self.fired.flags[gi] = false;
    self.fired.clear();
}
