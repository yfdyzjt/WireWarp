//! V3 wave-local fusion of sorted deterministic fault-trigger regions.
//! Control-only toggles commute within a wire phase; output and dirty order
//! are preserved. No region crosses a wave boundary.
const std = @import("std");
const Graph = @import("Graph.zig");
const PackedGates = @import("PackedGates.zig");

const Block = struct { word: usize, mask: u64 };
pub const block_bytes = @sizeOf(Block);
const Program = struct {
    first: usize = 0, end: usize = 0, blocks: []const Block = &.{},
    control_first: usize = 0, control_end: usize = 0,
    output_first: usize = 0, outputs: []const i32 = &.{},
};
// V3.5: compact per-wire descriptors and predecoded short target runs.
// Only monotonic normal-lamp/control/fault_1 sequences qualify; everything
// else uses the v3.2 interpreter (including probabilistic faults).
const FastKind = enum(u8) { fallback, short, control, outputs, output1 };
const FastPlan = struct {
    start: u32 = 0, len: u32 = 0, kind: FastKind = .fallback,
    normal: u8 = 0, controls: u8 = 0, faults: u8 = 0,
};
const ShortOp = struct { id: u32, owner: u32 = 0 };
const FastPlans = struct { plans: []FastPlan, ops: []ShortOp };
const no_lazy = std.math.maxInt(u32);
const Single = struct { lamp: ?*Graph.Lamp = null, lazy_wire: u32 = no_lazy, fault: bool = false };
const Regions = @This();
pub const Counters = struct {
    region_events: u64 = 0,
    waves: u64 = 0,
    wire_visits: u64 = 0,
    targets: u64 = 0,
    control_original: u64 = 0,
    control_executed: u64 = 0,
    masked_triggers: u64 = 0,
    mask_blocks: u64 = 0,
    batched_outputs: u64 = 0,
    lazy_wave_toggles: u64 = 0,
    lazy_commits: u64 = 0,
    pruned_visits: u64 = 0,
    pruned_triggers: u64 = 0,
    direct_lazy_visits: u64 = 0,
    skipped_fault_checks: u64 = 0,
    fast_wire_visits: u64 = 0,
    fast_short_ops: u64 = 0,
};
counters: Counters = .{},
programs: []Program,
output_storage: []const i32,
fast_plans: ?FastPlans = null,
fast_plan_count: usize = 0,
packed_gates: ?PackedGates = null,
words: []u64,
singles: []Single,
control_parity: []bool,
control_seen: []bool,
control_touched: []usize,
control_len: usize = 0,
// Each eligible group belongs to exactly one deferred control wire. Bit 0 stores
// the deferred XOR; bit 1 marks membership in the event-local touched list.
lazy_eligible: []bool,
lazy_state: []u8,
lazy_touched: []usize,
lazy_len: usize = 0,
lazy_count: usize = 0,
lazy_lamps: usize = 0,
control_count: usize = 0,
output_count: usize = 0,
region_count: usize = 0,
block_count: usize = 0,
trigger_count: usize = 0,
pruned_count: usize = 0,
pruned_blocks: usize = 0,
pruned_entries: usize = 0,
compact_policy: bool = false,

pub fn init(a: std.mem.Allocator, g: *const Graph) std.mem.Allocator.Error!Regions {
    return initPolicy(a, g, false, std.math.maxInt(usize), &.{});
}

// Omitted masks use the original ordered target loop. The allowlist affects
// optimization only, never which graph nodes are reachable or executable.
fn maskAllowed(wi: usize, entries: usize, cap: usize, allow: []const usize) bool {
    if (entries <= cap) return true;
    var lo: usize = 0;
    var hi = allow.len;
    while (lo < hi) {
        const mid = lo + (hi - lo) / 2;
        if (allow[mid] < wi) lo = mid + 1 else hi = mid;
    }
    return lo < allow.len and allow[lo] == wi;
}

pub fn initPolicy(a: std.mem.Allocator, g: *const Graph, compact: bool, cap: usize, allow: []const usize) std.mem.Allocator.Error!Regions {
    const programs = try a.alloc(Program, g.wires.len);
    errdefer a.free(programs);
    const words = try a.alloc(u64, (g.gates.len + 63) / 64);
    errdefer a.free(words);
    @memset(words, 0);
    const singles = try a.alloc(Single, g.gates.len);
    errdefer a.free(singles);
    for (g.gates, singles) |gate, *s| {
        s.* = if (gate.class == .gate_1 or gate.class == .fault_1)
            .{ .lamp = gate.lamps[0], .fault = gate.class == .fault_1 }
        else .{};
    }
    const control_parity = try a.alloc(bool, g.wires.len);
    errdefer a.free(control_parity);
    @memset(control_parity, false);
    const control_seen = try a.alloc(bool, g.wires.len);
    errdefer a.free(control_seen);
    @memset(control_seen, false);
    const control_touched = try a.alloc(usize, g.wires.len);
    errdefer a.free(control_touched);
    var control_count: usize = 0;
    var output_count: usize = 0;
    var output_total: usize = 0;
    // Count exact storage, then fill one contiguous allocation. A region is
    // accepted only if every fault trigger in the contiguous run is fault_1,
    // IDs are nondecreasing, and compression actually reduces operation count.
    var total: usize = 0;
    var count: usize = 0;
    var triggers: usize = 0;
    var pruned_count: usize = 0;
    var pruned_blocks: usize = 0;
    var pruned_entries: usize = 0;
    for (g.wires, programs, 0..) |wire, *p, wi| {
        p.* = .{};
        var cf: usize = 0;
        while (cf < wire.targets.len and std.meta.activeTag(wire.targets[cf]) != .lamp_on_fault) : (cf += 1) {}
        var ce = cf;
        while (ce < wire.targets.len and std.meta.activeTag(wire.targets[ce]) == .lamp_on_fault) : (ce += 1) {}
        if (ce - cf >= 8) {
            p.control_first = cf;
            p.control_end = ce;
            control_count += 1;
        }
        var of = wire.targets.len;
        while (of > 0 and std.meta.activeTag(wire.targets[of - 1]) == .output_port) : (of -= 1) {}
        if (wire.targets.len - of >= 2) {
            p.output_first = of;
            output_total += wire.targets.len - of;
            output_count += 1;
        } else p.output_first = wire.targets.len;
        var first: usize = 0;
        while (first < wire.targets.len and std.meta.activeTag(wire.targets[first]) != .fault_gate) : (first += 1) {}
        var end = first;
        var blocks: usize = 0;
        var prev: ?usize = null;
        var safe = true;
        while (end < wire.targets.len) : (end += 1) {
            const gi = switch (wire.targets[end]) { .fault_gate => |id| id, else => break };
            if (g.gates[gi].class != .fault_1) safe = false;
            if (prev) |last| {
                if (gi < last) safe = false;
                if (gi / 64 != last / 64) blocks += 1;
            } else blocks += 1;
            prev = gi;
        }
        if (safe and end - first >= 8 and blocks < end - first) {
            p.first = first;
            p.end = end;
            if (!compact or maskAllowed(wi, end - first, cap, allow)) {
                total += blocks;
                count += 1;
                triggers += end - first;
            } else {
                pruned_count += 1;
                pruned_blocks += blocks;
                pruned_entries += end - first;
            }
        }
    }
    const storage = try a.alloc(Block, total);
    errdefer a.free(storage);
    const output_storage = try a.alloc(i32, output_total);
    errdefer a.free(output_storage);
    var output_cursor: usize = 0;
    var cursor: usize = 0;
    for (g.wires, programs, 0..) |wire, *p, wi| {
        const output_start = output_cursor;
        for (wire.targets[p.output_first..]) |t| {
            output_storage[output_cursor] = t.output_port;
            output_cursor += 1;
        }
        p.outputs = output_storage[output_start..output_cursor];
        if (p.end == 0 or (compact and !maskAllowed(wi, p.end - p.first, cap, allow))) continue;
        const start = cursor;
        for (wire.targets[p.first..p.end]) |t| {
            const gi = t.fault_gate;
            const word = gi / 64;
            const bit = @as(u64, 1) << @as(u6, @intCast(gi % 64));
            if (cursor == start or storage[cursor - 1].word != word) {
                storage[cursor] = .{ .word = word, .mask = bit };
                cursor += 1;
            } else storage[cursor - 1].mask |= bit;
        }
        p.blocks = storage[start..cursor];
    }
    std.debug.assert(cursor == total);
    // Only one deferred group per lamp. Other ordinary writers may still
    // XOR the physical bit: physical XOR group parity is its logical value.
    // Overlapping large runs or duplicate targets within a run are rejected.
    // The compiler gives every normal lamp exactly one owning gate.
    const memberships = try a.alloc(u8, g.lamps.len);
    defer a.free(memberships);
    @memset(memberships, 0);
    for (g.wires, programs) |wire, p| {
        for (wire.targets[p.control_first..p.control_end]) |t| {
            memberships[t.lamp_on_fault] +|= 1;
        }
    }
    const lazy_eligible = try a.alloc(bool, g.wires.len);
    errdefer a.free(lazy_eligible);
    @memset(lazy_eligible, false);
    const lazy_state = try a.alloc(u8, g.wires.len);
    errdefer a.free(lazy_state);
    @memset(lazy_state, 0);
    const lazy_touched = try a.alloc(usize, control_count);
    var lazy_count: usize = 0;
    var lazy_lamps: usize = 0;
    for (g.wires, programs, 0..) |wire, p, wi| {
        if (p.control_end == 0 or wi >= no_lazy or
            (p.blocks.len != 0 and p.control_end > p.first)) continue;
        const run = wire.targets[p.control_first..p.control_end];
        var safe = true;
        for (run) |t| {
            const li = t.lamp_on_fault;
            const lamp = &g.lamps[li];
            const gi = lamp.gate;
            if (memberships[li] != 1 or gi >= g.gates.len or
                g.gates[gi].class != .fault_1 or
                g.gates[gi].lamps.len != 1 or g.gates[gi].lamps[0] != lamp) {
                safe = false;
                break;
            }
        }
        if (!safe) continue;
        lazy_eligible[wi] = true;
        lazy_count += 1;
        lazy_lamps += run.len;
        for (run) |t| singles[g.lamps[t.lamp_on_fault].gate].lazy_wire = @intCast(wi);
    }
    return .{ .programs = programs, .output_storage = output_storage, .words = words, .singles = singles, .region_count = count, .block_count = total, .trigger_count = triggers, .control_parity = control_parity, .control_seen = control_seen, .control_touched = control_touched, .control_count = control_count, .output_count = output_count, .lazy_eligible = lazy_eligible, .lazy_state = lazy_state, .lazy_touched = lazy_touched, .lazy_count = lazy_count, .lazy_lamps = lazy_lamps, .pruned_count = pruned_count, .pruned_blocks = pruned_blocks, .pruned_entries = pruned_entries, .compact_policy = compact };
}

/// Compile once for the exact same graph as the v3.2 regions. The compact
/// descriptors are independent of simulation state, and never reorder targets.
pub fn initFastPlans(self: *Regions, a: std.mem.Allocator, g: *const Graph) std.mem.Allocator.Error!void {
    if (self.fast_plans != null) return;
    const plans = try a.alloc(FastPlan, g.wires.len);
    errdefer a.free(plans);
    var ops: std.ArrayList(ShortOp) = .empty;
    errdefer ops.deinit(a);
    var output_cursor: usize = 0;
    var plan_count: usize = 0;
    for (g.wires, self.programs, plans) |wire, p, *plan| {
        plan.* = .{};
        const ts = wire.targets;
        const output_start = output_cursor;
        output_cursor += p.outputs.len;
        if (ts.len > std.math.maxInt(u32)) continue;
        if (p.control_end != 0 and p.control_first == 0 and p.control_end == ts.len) {
            plan.* = .{ .kind = .control, .len = @intCast(ts.len) };
        } else if (p.output_first == 0 and p.outputs.len != 0 and output_start <= std.math.maxInt(u32)) {
            plan.* = .{ .kind = .outputs, .start = @intCast(output_start), .len = @intCast(p.outputs.len) };
        } else if (ts.len == 1 and std.meta.activeTag(ts[0]) == .output_port) {
            plan.* = .{ .kind = .output1, .start = @bitCast(ts[0].output_port), .len = 1 };
        } else if (ts.len > 0 and ts.len <= 7 and ops.items.len <= std.math.maxInt(u32) - ts.len) {
            var scratch: [7]ShortOp = undefined;
            var counts = [_]u8{0} ** 3;
            var previous: usize = 0;
            var safe = true;
            for (ts, 0..) |t, i| {
                const group: usize = switch (t) { .lamp => 0, .lamp_on_fault => 1, .fault_gate => 2, else => 3 };
                if (group < previous or group == 3) { safe = false; break; }
                previous = group;
                const id: usize = switch (t) { .lamp => |v| v, .lamp_on_fault => |v| v, .fault_gate => |v| v, else => unreachable };
                if (id > std.math.maxInt(u32) or (group == 2 and g.gates[id].class != .fault_1)) { safe = false; break; }
                scratch[i] = .{ .id = @intCast(id), .owner = if (group == 0) g.lamps[id].gate else 0 };
                counts[group] += 1;
            }
            if (!safe) continue;
            const start = ops.items.len;
            try ops.appendSlice(a, scratch[0..ts.len]);
            plan.* = .{ .kind = .short, .start = @intCast(start), .len = @intCast(ts.len), .normal = counts[0], .controls = counts[1], .faults = counts[2] };
        }
        if (plan.kind != .fallback) plan_count += 1;
    }
    self.fast_plans = .{ .plans = plans, .ops = try ops.toOwnedSlice(a) };
    self.fast_plan_count = plan_count;
}

pub inline fn tripFast(self: *Regions, sim: anytype, wi: usize, comptime profile: bool) std.mem.Allocator.Error!bool {
    const fast = self.fast_plans orelse return false;
    const p = fast.plans[wi];
    switch (p.kind) {
        .fallback => return false,
        .control => self.queueControlCount(wi, p.len, profile),
        .outputs => {
            const start: usize = p.start;
            try sim.out.appendSlice(sim.alloc, self.output_storage[start..][0..p.len]);
            if (profile) self.counters.batched_outputs += p.len;
        },
        .output1 => try sim.out.append(sim.alloc, @bitCast(p.start)),
        .short => {
            const start: usize = p.start;
            const ops = fast.ops[start..][0..p.len];
            const normal_end: usize = p.normal;
            const control_end = normal_end + p.controls;
            for (ops[0..normal_end]) |op| {
                const cell = &sim.g.lamps[op.id];
                cell.on = !cell.on;
                self.push(&sim.dirty, op.owner);
            }
            for (ops[normal_end..control_end]) |op| {
                const cell = &sim.g.lamps[op.id];
                cell.on = !cell.on;
            }
            for (ops[control_end..]) |op| self.push(&sim.dirty, op.id);
            if (profile) self.counters.fast_short_ops += p.len;
        },
    }
    if (profile) {
        self.counters.fast_wire_visits += 1;
        self.counters.wire_visits += 1;
        self.counters.targets += p.len;
    }
    return true;
}

pub inline fn push(self: *Regions, dirty: anytype, gi: usize) void {
    const word = gi / 64;
    const bit = @as(u64, 1) << @as(u6, @intCast(gi % 64));
    if (self.words[word] & bit != 0) return;
    self.words[word] |= bit;
    dirty.items[dirty.len] = gi;
    dirty.len += 1;
}

fn apply(self: *Regions, dirty: anytype, blocks: []const Block) void {
    for (blocks) |b| {
        const old = self.words[b.word];
        var fresh = b.mask & ~old;
        if (fresh == 0) continue;
        self.words[b.word] = old | b.mask;
        // Sorted IDs within a run; append only newly dirty gates, in the
        // same first-seen order as individual Set.push calls.
        while (fresh != 0) {
            dirty.items[dirty.len] = b.word * 64 + @as(usize, @ctz(fresh));
            dirty.len += 1;
            fresh &= fresh - 1;
        }
    }
}

fn targets(self: *Regions, sim: anytype, ts: []const Graph.Target) std.mem.Allocator.Error!void {
    for (ts) |t| switch (t) {
        .lamp => |li| {
            const cell = &sim.g.lamps[li];
            cell.on = !cell.on;
            self.push(&sim.dirty, cell.gate);
        },
        .lamp_on_fault => |li| {
            const cell = &sim.g.lamps[li];
            cell.on = !cell.on;
        },
        .fault_gate => |gi| {
            if (sim.g.gates[gi].class == .fault_n) sim.fault_hits[gi] += 1;
            self.push(&sim.dirty, gi);
        },
        .output_port => |p| try sim.out.append(sim.alloc, p),
    };
}

inline fn queueControl(self: *Regions, wi: usize, p: Program, comptime profile: bool) void {
    self.queueControlCount(wi, p.control_end - p.control_first, profile);
}

inline fn queueControlCount(self: *Regions, wi: usize, count: usize, comptime profile: bool) void {
    if (!self.control_seen[wi]) {
        self.control_seen[wi] = true;
        self.control_touched[self.control_len] = wi;
        self.control_len += 1;
    }
    self.control_parity[wi] = !self.control_parity[wi];
    if (profile) self.counters.control_original += @intCast(count);
}

inline fn toggleLazy(self: *Regions, wi: usize) void {
    const state = &self.lazy_state[wi];
    if (state.* == 0) {
        self.lazy_touched[self.lazy_len] = wi;
        self.lazy_len += 1;
    }
    state.* = (state.* ^ 1) | 2;
}

pub fn trip(self: *Regions, sim: anytype, wi: usize, comptime profile: bool, comptime compact: bool, comptime direct: bool) std.mem.Allocator.Error!void {
    const p = self.programs[wi];
    const ts = sim.g.wires[wi].targets;
    if (profile) {
        self.counters.wire_visits += 1;
        self.counters.targets += @intCast(ts.len);
        if (p.end != 0 and p.blocks.len == 0) {
            self.counters.pruned_visits += 1;
            self.counters.pruned_triggers += @intCast(p.end - p.first);
        }
    }
    // Common RGB wire shapes need no general target scanning. Keep the
    // same parity queue and ordered append, including duplicate outputs.
    if (compact) {
        if (p.control_end != 0 and p.control_first == 0 and p.control_end == ts.len) {
            // A pure lazy control wire has no dirty/output side effects.
            // No gate observes its state until the entire wire phase ends.
            if (direct and self.lazy_eligible[wi]) {
                self.toggleLazy(wi);
                if (profile) {
                    self.counters.control_original += @intCast(ts.len);
                    self.counters.direct_lazy_visits += 1;
                }
            } else self.queueControl(wi, p, profile);
            return;
        }
        if (p.output_first == 0 and p.outputs.len != 0) {
            try sim.out.appendSlice(sim.alloc, p.outputs);
            if (profile) self.counters.batched_outputs += @intCast(p.outputs.len);
            return;
        }
    }
    var cursor: usize = 0;
    // Compiled targets have control runs before fault runs. On hand-built
    // graphs with a reversed order, use the ordinary target path instead.
    if (p.control_end != 0 and (p.blocks.len == 0 or p.control_end <= p.first)) {
        try self.targets(sim, ts[0..p.control_first]);
        self.queueControl(wi, p, profile);
        cursor = p.control_end;
    }
    if (p.blocks.len != 0) {
        try self.targets(sim, ts[cursor..p.first]);
        self.apply(&sim.dirty, p.blocks);
        if (profile) {
            self.counters.masked_triggers += @intCast(p.end - p.first);
            self.counters.mask_blocks += @intCast(p.blocks.len);
        }
        cursor = p.end;
    }
    try self.targets(sim, ts[cursor..p.output_first]);
    if (p.outputs.len != 0) {
        try sim.out.appendSlice(sim.alloc, p.outputs);
        if (profile) self.counters.batched_outputs += @intCast(p.outputs.len);
    }
}

pub fn endWirePhase(self: *Regions, sim: anytype, comptime profile: bool, comptime lazy: bool) void {
    if (profile) self.counters.waves += 1;
    // Control lamps never dirty gates. No gate reads them until all wire
    // targets have been processed. Odd visits toggle once; even visits cancel.
    for (self.control_touched[0..self.control_len]) |wi| {
        if (self.control_parity[wi]) {
            const p = self.programs[wi];
            if (lazy and self.lazy_eligible[wi]) {
                self.toggleLazy(wi);
                if (profile) self.counters.lazy_wave_toggles += 1;
            } else self.materialize(sim, wi, p, profile);
        }
        self.control_parity[wi] = false;
        self.control_seen[wi] = false;
    }
    self.control_len = 0;
    const dirty = &sim.dirty;
    // No dirties are inserted in the gate phase. Clear the bitset at the
    // phase boundary, not when processing individual wire regions.
    if (dirty.len < self.words.len) {
        for (dirty.slice()) |gi| self.words[gi / 64] = 0;
    } else @memset(self.words, 0);
}

fn materialize(self: *Regions, sim: anytype, wi: usize, p: Program, comptime profile: bool) void {
    if (profile) self.counters.control_executed += @intCast(p.control_end - p.control_first);
    for (sim.g.wires[wi].targets[p.control_first..p.control_end]) |t| {
        const cell = &sim.g.lamps[t.lamp_on_fault];
        cell.on = !cell.on;
    }
}

/// Physical graph state must be exact at every successful event boundary,
/// including before a subsequent generic or whole-root fused event.
pub fn endEvent(self: *Regions, sim: anytype, comptime profile: bool) void {
    for (self.lazy_touched[0..self.lazy_len]) |wi| {
        if (self.lazy_state[wi] & 1 != 0) {
            self.materialize(sim, wi, self.programs[wi], profile);
            if (profile) self.counters.lazy_commits += 1;
        }
        self.lazy_state[wi] = 0;
    }
    self.lazy_len = 0;
}

pub inline fn evaluate(self: *const Regions, sim: anytype, gi: usize, comptime lazy: bool) ?bool {
    const s = self.singles[gi];
    const lamp = s.lamp orelse return null;
    const cur = lamp.on;
    if (s.fault) {
        if (lazy and s.lazy_wire != no_lazy) return cur != (self.lazy_state[s.lazy_wire] & 1 != 0);
        return cur;
    }
    const changed = cur != sim.gate_state[gi];
    sim.gate_state[gi] = cur;
    return changed;
}

/// Retain the original Single table for overflow and reference fallback.
pub fn initPackedGates(self: *Regions, a: std.mem.Allocator, g: *const Graph) std.mem.Allocator.Error!void {
    if (self.packed_gates != null) return;
    const plans = try a.alloc(PackedGates.Plan, g.gates.len);
    var table: PackedGates = .{ .plans = plans };
    for (g.gates, self.singles, plans) |gate, single, *plan| {
        const wi: ?u32 = if (single.lazy_wire != no_lazy) single.lazy_wire else null;
        plan.* = PackedGates.makePlan(g, gate, wi);
        table.counts[@intFromEnum(plan.op)] += 1;
    }
    self.packed_gates = table;
}
