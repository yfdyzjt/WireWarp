const Sim = @This();

const std = @import("std");
const ArrayList = std.ArrayList;
const Set = @import("../util.zig").Set;
const Graph = @import("Graph.zig");
const Aot = @import("AotGenerated.zig");
const Regions = @import("Regions.zig");
const Continuations = @import("Continuations.zig");
pub const continuation_mode = if (@hasDecl(Aot, "continuations")) Aot.continuations else false;
pub const compiled_digest = Aot.digest;
pub const compiled_specialization = Aot.enabled;
pub const region_mode = if (@hasDecl(Aot, "regions")) Aot.regions else false;
pub const lazy_mode = if (@hasDecl(Aot, "lazy_controls")) Aot.lazy_controls else false;
pub const compact_mode = if (@hasDecl(Aot, "compact_regions")) Aot.compact_regions else false;
pub const direct_mode = if (@hasDecl(Aot, "direct_regions")) Aot.direct_regions else false;
pub const packed_mode = if (@hasDecl(Aot, "packed_gates")) Aot.packed_gates else false;
pub const fast_mode = if (@hasDecl(Aot, "fast_wires")) Aot.fast_wires else false;
pub const profiling_release = if (@hasDecl(Aot, "profiling_release")) Aot.profiling_release else false;
const mask_cap: usize = if (@hasDecl(Aot, "mask_cap")) Aot.mask_cap else 2048;
const mask_allow: []const usize = if (@hasDecl(Aot, "mask_allow")) Aot.mask_allow else &.{};

alloc: std.mem.Allocator,
g: *const Graph,
rng: std.Random,
prng: *std.Random.DefaultPrng,
regions: ?Regions = null,
regions_enabled: bool = false,
lazy_enabled: bool = false,
compact_enabled: bool = false,
direct_enabled: bool = false,
fast_enabled: bool = false,
packed_enabled: bool = false,
continuation_enabled: bool = false,
continuations: ?Continuations = null,
continuation_counts: [4]u64 = [_]u64{0} ** 4,
packed_checks: u64 = 0,
packed_fallback_checks: u64 = 0,
profile_regions: bool = false,
gate_state: []bool,
dirty: Set,
fault_hits: []u32,
fired: Set,

target_groups: ArrayList([]const usize),
out: ArrayList(i32),

aot_enabled: bool = false,

events: u64 = 0,
checks: u64 = 0,
fused_events: u64 = 0,

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
        .prng = prng,
        .gate_state = gate_state,
        .dirty = .{ .flags = dirty_flag, .items = dirty_gates },
        .fault_hits = fault_hits,
        .fired = .{ .flags = fired_flag, .items = fired_list },
        .target_groups = try ArrayList([]const usize).initCapacity(a, @max(gate_count, 1)),
        .out = try ArrayList(i32).initCapacity(a, 64),
    };
}

/// Activate only for the exact serialized graph used during code generation.
pub fn enableAot(self: *Sim, wwir: []const u8) bool {
    if (continuation_mode) {
        if (self.enableAotV41(wwir)) return true;
        if (self.enableAotV40(wwir)) return true;
        return self.enableAotV35(wwir);
    }
    if (packed_mode) {
        if (self.enableAotV40(wwir)) return true;
        return self.enableAotV35(wwir);
    }
    if (fast_mode) return self.enableAotV35(wwir);
    if (profiling_release) return self.enableAotV32(wwir);
    if (direct_mode) return self.enableAotV33(wwir);
    return self.enableAotPolicy(wwir, region_mode, lazy_mode, compact_mode);
}

/// Benchmark v2 without constructing v3 tables. Hash guard applies to both.
pub fn enableAotMode(self: *Sim, wwir: []const u8, use_regions: bool) bool {
    return self.enableAotVariant(wwir, use_regions, false);
}

/// V3 remains an explicit comparison path; V3.1 opts into lazy controls.
pub fn enableAotVariant(self: *Sim, wwir: []const u8, use_regions: bool, use_lazy: bool) bool {
    return self.enableAotPolicy(wwir, use_regions, use_lazy, false);
}

pub fn enableAotV32(self: *Sim, wwir: []const u8) bool {
    return self.enableAotPolicy(wwir, true, true, true);
}

pub fn enableAotV33(self: *Sim, wwir: []const u8) bool {
    if (!direct_mode) return false;
    const ok = self.enableAotV32(wwir);
    self.direct_enabled = ok and self.regions_enabled;
    return ok;
}

pub fn enableAotV35(self: *Sim, wwir: []const u8) bool {
    if (!fast_mode) return false;
    const ok = self.enableAotV32(wwir);
    if (ok and self.regions_enabled) {
        self.regions.?.initFastPlans(self.alloc, self.g) catch {
            self.aot_enabled = false;
            self.regions_enabled = false;
            return false;
        };
        self.fast_enabled = self.regions.?.fast_plan_count != 0;
    }
    return ok;
}

/// Explicit comparison path. If allocation fails, the live caller falls back to V3.5.
pub fn enableAotV40(self: *Sim, wwir: []const u8) bool {
    if (!packed_mode) return false;
    if (!self.enableAotV35(wwir)) return false;
    if (self.regions) |*r| {
        r.initPackedGates(self.alloc, self.g) catch return false;
        self.packed_enabled = true;
        return true;
    }
    return false;
}

/// V4.1 is additive: if plan allocation fails, retain the validated V4.0 path.
pub fn enableAotV41(self: *Sim, wwir: []const u8) bool {
    if (!continuation_mode) return false;
    if (!self.enableAotV40(wwir)) return false;
    if (self.continuations == null) self.continuations = Continuations.init(self.alloc, self.g) catch return false;
    self.continuation_enabled = true;
    return true;
}

fn enableAotPolicy(self: *Sim, wwir: []const u8, use_regions: bool, use_lazy: bool, compact: bool) bool {
    self.aot_enabled = false;
    self.regions_enabled = false;
    self.compact_enabled = false;
    self.direct_enabled = false;
    self.fast_enabled = false;
    self.packed_enabled = false;
    self.continuation_enabled = false;
    self.lazy_enabled = false;
    if (!Aot.enabled or (compact and !compact_mode)) return false;
    var digest: [32]u8 = undefined;
    std.crypto.hash.sha2.Sha256.hash(wwir, &digest, .{});
    self.aot_enabled = std.mem.eql(u8, &digest, &Aot.digest);
    if (self.aot_enabled and region_mode and use_regions) {
        // Each benchmark variant owns a fresh arena and graph. Do not
        // silently reuse tables constructed for a different mask policy.
        if (self.regions) |r| {
            if (r.compact_policy != compact) {
                self.aot_enabled = false;
                return false;
            }
        }
        if (self.regions == null) self.regions = Regions.initPolicy(self.alloc, self.g, compact, mask_cap, mask_allow) catch {
            self.aot_enabled = false;
            return false;
        };
        const r = &self.regions.?;
        // Avoid the region scheduler entirely on a graph with no compiled
        // regions. Preserve the previous v3 path for benchmark comparisons.
        self.regions_enabled = !use_lazy or r.region_count != 0 or r.control_count != 0 or r.output_count != 0;
        self.lazy_enabled = use_lazy and r.lazy_count != 0;
        self.compact_enabled = compact;
        // A non-lazy control-only graph may benefit from v3 parity, but
        // the conservative v3.2 policy uses v2 unless another region exists.
        if (compact) self.regions_enabled = r.region_count != 0 or r.lazy_count != 0 or r.output_count != 0;
    }
    return self.aot_enabled;
}

pub fn reset(self: *Sim) void {
    for (self.g.lamps) |*l| l.on = l.initial;
    for (self.g.gates, 0..) |gate, i| self.gate_state[i] = gate.state;
    self.dirty.reset();
    if (self.regions) |*r| {
        @memset(r.words, 0);
        @memset(r.control_seen, false);
        @memset(r.control_parity, false);
        r.control_len = 0;
        @memset(r.lazy_state, 0);
        r.lazy_len = 0;
    }
    @memset(self.fault_hits, 0);
    self.clearEvent();
}

/// Applies one input event and returns the output ports it produced.
/// The returned slice is only valid until the next `event` call; the caller
/// must copy it with its own allocator if it needs to retain it.
pub const Stage = enum { clear, dispatch, wire, flush, gates, commit };
/// Separate instrumented instantiation: no clock calls in ordinary event().
pub const StageProfile = struct {
    io: std.Io,
    last_ns: i96 = 0,
    ns: [6]i96 = [_]i96{0} ** 6,
    reads: u64 = 0,
    events: u64 = 0,
    waves: u64 = 0,
    fused: u64 = 0,
    max_event_ns: i96 = 0,
    event_start: i96 = 0,

    fn begin(p: *StageProfile) void {
        p.last_ns = std.Io.Clock.now(.awake, p.io).nanoseconds;
        p.event_start = p.last_ns;
        p.reads += 1;
        p.events += 1;
    }
    fn mark(p: *StageProfile, stage: Stage) void {
        const now = std.Io.Clock.now(.awake, p.io).nanoseconds;
        p.ns[@intFromEnum(stage)] += now - p.last_ns;
        p.last_ns = now;
        p.reads += 1;
    }
    fn finish(p: *StageProfile) void {
        p.max_event_ns = @max(p.max_event_ns, p.last_ns - p.event_start);
    }
};

pub fn event(self: *Sim, port: i32) std.mem.Allocator.Error![]const i32 {
    return self.eventImpl(port, false, null);
}

pub fn eventProfiled(self: *Sim, port: i32, stages: *StageProfile) std.mem.Allocator.Error![]const i32 {
    return self.eventImpl(port, true, stages);
}

fn eventImpl(self: *Sim, port: i32, comptime timed: bool, stages: ?*StageProfile) std.mem.Allocator.Error![]const i32 {
    if (timed) stages.?.begin();
    self.events += 1;
    self.clearEvent();
    std.debug.assert(self.dirty.len == 0);
    if (timed) stages.?.mark(.clear);
    // Only complete, proven state-independent root schedules may bypass the scheduler.
    if (Aot.fused and self.aot_enabled) {
        if (try Aot.event(self, port)) {
            self.fused_events += 1;
            if (timed) {
                stages.?.fused += 1;
                stages.?.mark(.dispatch);
                stages.?.finish();
            }
            return self.out.items;
        }
    }
    if (self.continuation_enabled and self.regions_enabled) {
        if (self.profile_regions) {
            self.regions.?.counters.region_events += 1;
            if (self.lazy_enabled) return self.runWaves(port, true, true, true, true, false, true, true, true, timed, stages);
            return self.runWaves(port, true, true, false, true, false, true, true, true, timed, stages);
        }
        if (self.lazy_enabled) return self.runWaves(port, true, false, true, true, false, true, true, true, timed, stages);
        return self.runWaves(port, true, false, false, true, false, true, true, true, timed, stages);
    }
    if (self.packed_enabled) {
        if (self.regions_enabled) {
            if (self.profile_regions) {
                self.regions.?.counters.region_events += 1;
                if (self.lazy_enabled) return self.runWaves(port, true, true, true, true, false, true, true, false, timed, stages);
                return self.runWaves(port, true, true, false, true, false, true, true, false, timed, stages);
            }
            if (self.lazy_enabled) return self.runWaves(port, true, false, true, true, false, true, true, false, timed, stages);
            return self.runWaves(port, true, false, false, true, false, true, true, false, timed, stages);
        }
        // No region scheduler: preserve generic dirty flags and order.
        if (self.profile_regions) return self.runWaves(port, false, true, false, false, false, false, true, false, timed, stages);
        return self.runWaves(port, false, false, false, false, false, false, true, false, timed, stages);
    }
    if (self.regions_enabled) {
        if (self.fast_enabled) {
            if (self.profile_regions) {
                self.regions.?.counters.region_events += 1;
                if (self.lazy_enabled) return self.runWaves(port, true, true, true, true, false, true, false, false, timed, stages);
                return self.runWaves(port, true, true, false, true, false, true, false, false, timed, stages);
            }
            if (self.lazy_enabled) return self.runWaves(port, true, false, true, true, false, true, false, false, timed, stages);
            return self.runWaves(port, true, false, false, true, false, true, false, false, timed, stages);
        }
        if (self.direct_enabled) {
            if (self.profile_regions) {
                self.regions.?.counters.region_events += 1;
                if (self.lazy_enabled) return self.runWaves(port, true, true, true, true, true, false, false, false, timed, stages);
                return self.runWaves(port, true, true, false, true, true, false, false, false, timed, stages);
            }
            if (self.lazy_enabled) return self.runWaves(port, true, false, true, true, true, false, false, false, timed, stages);
            return self.runWaves(port, true, false, false, true, true, false, false, false, timed, stages);
        }
        if (self.compact_enabled) {
            if (self.profile_regions) {
                self.regions.?.counters.region_events += 1;
                if (self.lazy_enabled) return self.runWaves(port, true, true, true, true, false, false, false, false, timed, stages);
                return self.runWaves(port, true, true, false, true, false, false, false, false, timed, stages);
            }
            if (self.lazy_enabled) return self.runWaves(port, true, false, true, true, false, false, false, false, timed, stages);
            return self.runWaves(port, true, false, false, true, false, false, false, false, timed, stages);
        }
        if (self.profile_regions) {
            self.regions.?.counters.region_events += 1;
            if (self.lazy_enabled) return self.runWaves(port, true, true, true, false, false, false, false, false, timed, stages);
            return self.runWaves(port, true, true, false, false, false, false, false, false, timed, stages);
        }
        if (self.lazy_enabled) return self.runWaves(port, true, false, true, false, false, false, false, false, timed, stages);
        return self.runWaves(port, true, false, false, false, false, false, false, false, timed, stages);
    }
    return self.runWaves(port, false, false, false, false, false, false, false, false, timed, stages);
}

fn runWaves(self: *Sim, port: i32, comptime use_regions: bool, comptime profile: bool, comptime lazy: bool, comptime compact: bool, comptime direct: bool, comptime fast_wires: bool, comptime packed_eval: bool, comptime continuation: bool, comptime timed: bool, stages: ?*StageProfile) std.mem.Allocator.Error![]const i32 {
    if (port >= 0 and port < @as(i32, @intCast(self.g.input_ports.len))) {
        self.target_groups.appendAssumeCapacity(self.g.input_ports[@intCast(port)].wires);
    }

    if (timed) stages.?.mark(.dispatch);
    while (self.target_groups.items.len > 0 or (continuation and self.continuations.?.pending.items.len > 0)) {
        if (timed) stages.?.waves += 1;
        for (self.target_groups.items) |wires| {
            for (wires) |w| {
                if (use_regions) {
                    if (fast_wires and try self.regions.?.tripFast(self, w, profile)) continue;
                    try self.regions.?.trip(self, w, profile, compact, direct and lazy);
                    continue;
                }
                if (!Aot.fused and self.aot_enabled and try Aot.trip(self, w)) continue;
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
        self.target_groups.clearRetainingCapacity();
        if (continuation) {
            // All entries were produced in the preceding gate phase. The
            // queue is read-only until the next gate phase begins.
            const queue = &self.continuations.?.pending;
            for (queue.items) |p| {
                switch (p.count()) {
                    0 => for (self.g.gates[p.gate()].wires) |w| { try self.tripContinuation(w, profile, compact); },
                    1 => try self.tripContinuation(p.wire(0), profile, compact),
                    2 => {
                        try self.tripContinuation(p.wire(0), profile, compact);
                        try self.tripContinuation(p.wire(1), profile, compact);
                    },
                    3 => {
                        try self.tripContinuation(p.wire(0), profile, compact);
                        try self.tripContinuation(p.wire(1), profile, compact);
                        try self.tripContinuation(p.wire(2), profile, compact);
                    },
                }
            }
            queue.clearRetainingCapacity();
        }

        if (timed) stages.?.mark(.wire);
        if (use_regions) self.regions.?.endWirePhase(self, profile, lazy);
        if (timed) stages.?.mark(.flush);
        for (self.dirty.slice()) |gi| {
            if (!use_regions) self.dirty.flags[gi] = false;
            self.checks += 1;
            // fault_1 evaluation is a pure read; a fired gate cannot emit
            // again this event. Never skip normal state updates or fault_n RNG.
            if (direct and self.fired.contains(gi) and self.g.gates[gi].class == .fault_1) {
                if (profile) self.regions.?.counters.skipped_fault_checks += 1;
                continue;
            }
            const result = if (packed_eval) self.evaluatePacked(gi, use_regions, lazy, profile) else self.evaluate(gi, use_regions, lazy);
            if (result and !self.fired.contains(gi)) {
                self.fired.push(gi);
                if (continuation) {
                    const c = &self.continuations.?;
                    const p = c.plans[gi];
                    c.pending.appendAssumeCapacity(p);
                    if (profile) self.continuation_counts[p.count()] += 1;
                } else self.target_groups.appendAssumeCapacity(self.g.gates[gi].wires);
            }
        }
        self.dirty.clear();
        if (timed) stages.?.mark(.gates);
    }

    if (lazy) self.regions.?.endEvent(self, profile);
    if (timed) {
        stages.?.mark(.commit);
        stages.?.finish();
    }
    return self.out.items;
}

inline fn tripContinuation(self: *Sim, w: usize, comptime profile: bool, comptime compact: bool) std.mem.Allocator.Error!void {
    if (try self.regions.?.tripFast(self, w, profile)) return;
    try self.regions.?.trip(self, w, profile, compact, false);
}

inline fn evaluatePacked(self: *Sim, gi: usize, comptime use_regions: bool, comptime lazy: bool, comptime profile: bool) bool {
    const r = &self.regions.?;
    if (r.packed_gates.?.evaluate(self, gi, r.lazy_state, lazy)) |result| {
        if (profile) self.packed_checks += 1;
        return result;
    }
    if (profile) self.packed_fallback_checks += 1;
    return self.evaluate(gi, use_regions, lazy);
}

fn evaluate(self: *Sim, gi: usize, comptime use_regions: bool, comptime lazy: bool) bool {
    if (use_regions) {
        if (self.regions.?.evaluate(self, gi, lazy)) |result| return result;
    }
    if (!Aot.fused and self.aot_enabled) {
        if (Aot.evaluate(self, gi)) |result| return result;
    }
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
    self.out.clearRetainingCapacity();
    self.target_groups.clearRetainingCapacity();
    if (self.continuations) |*c| c.pending.clearRetainingCapacity();
    // Reset only the flags this event actually set: a full-array memset
    // costs O(gate_count) cache-polluting writes per event.
    for (self.fired.slice()) |gi| self.fired.flags[gi] = false;
    self.fired.clear();
}
