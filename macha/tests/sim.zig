const std = @import("std");
const macha = @import("macha");

const Graph = macha.Graph;
const Sim = macha.Sim;
const Gate = Graph.Gate;
const Class = Graph.Gate.Class;
const Target = Graph.Target;
const Lamp = Graph.Lamp;
const Wire = Graph.Wire;
const InputPort = Graph.InputPort;

const testing = std.testing;

fn lampOf(gate: u32, initial: bool) Lamp {
    return .{ .gate = gate, .on = initial, .initial = initial };
}

fn gateOf(a: std.mem.Allocator, class: Class, lamps: []Lamp, indices: []const usize, wires: []const usize, state: bool) !Gate {
    const refs = try a.alloc(*Lamp, indices.len);
    for (indices, 0..) |li, k| refs[k] = &lamps[li];
    return .{ .class = class, .lamps = refs, .wires = wires, .state = state };
}

fn makeWires(a: std.mem.Allocator, specs: []const struct {
    lamps: []const usize,
    fault_gates: []const usize,
    outputs: []const i32,
}) ![]Wire {
    const wires = try a.alloc(Wire, specs.len);
    for (specs, 0..) |s, i| {
        const targets = try a.alloc(Target, s.lamps.len + s.fault_gates.len + s.outputs.len);
        var j: usize = 0;
        for (s.lamps) |li| {
            targets[j] = .{ .lamp = li };
            j += 1;
        }
        for (s.fault_gates) |gi| {
            targets[j] = .{ .fault_gate = gi };
            j += 1;
        }
        for (s.outputs) |p| {
            targets[j] = .{ .output_port = p };
            j += 1;
        }
        wires[i] = .{ .targets = targets };
    }
    return wires;
}

fn makeGraph(a: std.mem.Allocator, lamps: []Lamp, gates: []Gate, wires: []Wire, ports: []const struct { port: i32, wires: []const usize }) !Graph {
    const input_ports = try a.alloc(InputPort, ports.len);
    for (ports, 0..) |p, i| {
        std.debug.assert(p.port == @as(i32, @intCast(i)));
        const ws = try a.alloc(usize, p.wires.len);
        @memcpy(ws, p.wires);
        input_ports[i] = .{ .wires = ws };
    }
    return .{
        .wires = wires,
        .lamps = lamps,
        .gates = gates,
        .input_ports = input_ports,
    };
}

fn run(a: std.mem.Allocator, g: *const Graph, port: i32) ![]const i32 {
    var sim = try Sim.init(a, g, 1);
    return try sim.event(port);
}

test "gate_2_and: AND pulses only on state change" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 2);
    lamps[0] = lampOf(0, false);
    lamps[1] = lampOf(0, false);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_2_and, lamps, &.{ 0, 1 }, &.{2}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{1}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
        .{ .port = 1, .wires = &.{1} },
    });

    var sim = try Sim.init(a, &g, 1);
    try testing.expectEqualSlices(i32, &.{}, try sim.event(0)); // lamp 0 on: AND(1,2)=false → no pulse
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(1)); // lamp 1 on: AND(2,2)=true → pulse
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(0)); // lamp 0 off: false → change → pulse
    try testing.expectEqualSlices(i32, &.{}, try sim.event(1)); // lamp 1 off: false == false → silent
}

test "gate_2_xor: pulses on every toggle" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 2);
    lamps[0] = lampOf(0, false);
    lamps[1] = lampOf(0, false);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_2_xor, lamps, &.{ 0, 1 }, &.{2}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{1}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
        .{ .port = 1, .wires = &.{1} },
    });
    var sim = try Sim.init(a, &g, 1);
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(0)); // XOR(1,0)=true vs false → pulse
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(1)); // XOR(1,1)=false vs true → pulse
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(0)); // XOR(0,1)=true vs false → pulse
}

test "gate_1 relay: double hit silent, toggles pulse" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 1);
    lamps[0] = lampOf(0, false);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_1, lamps, &.{0}, &.{2}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{ 0, 1 } }, // double hit → net zero
        .{ .port = 1, .wires = &.{0} }, // single hit
    });
    var sim = try Sim.init(a, &g, 1);
    var all = try std.ArrayList(i32).initCapacity(a, 16);
    for ([_]i32{ 0, 1, 1, 0, 1 }) |port| {
        try all.appendSlice(a, try sim.event(port));
    }
    try testing.expectEqualSlices(i32, &.{ 7, 7, 7 }, all.items);
}

test "gate_n_and: NOR pulses on state change" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 3);
    lamps[0] = lampOf(0, true);
    lamps[1] = lampOf(0, true);
    lamps[2] = lampOf(0, true);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_n_and, lamps, &.{ 0, 1, 2 }, &.{1}, true);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{ 0, 1, 2 }, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
    });
    try testing.expectEqualSlices(i32, &.{7}, try run(a, &g, 0));
    try testing.expectEqualSlices(i32, &.{7}, try run(a, &g, 0));
}

test "fault_1 pass gate: fires iff parameter lamp lit" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 1);
    lamps[0] = lampOf(0, false); // parameter
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .fault_1, lamps, &.{0}, &.{2}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{} }, // parameter wire
        .{ .lamps = &.{}, .fault_gates = &.{0}, .outputs = &.{} }, // fault-lamp wire
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const parameter_targets = try a.alloc(Target, 1);
    parameter_targets[0] = .{ .lamp_on_fault = 0 };
    wires[0] = .{ .targets = parameter_targets };
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
        .{ .port = 1, .wires = &.{1} },
    });
    var sim = try Sim.init(a, &g, 1);

    try testing.expectEqualSlices(i32, &.{}, try sim.event(1)); // trigger with param off → silent
    try testing.expectEqualSlices(i32, &.{}, try sim.event(0)); // param on (no fire: parameters never pulse)
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(1)); // trigger with param on → fires
    _ = try sim.event(0); // param off again
    try testing.expectEqualSlices(i32, &.{}, try sim.event(1)); // trigger with param off → silent
}

test "fault_n: rolls compound per hit" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    const B = struct {
        fn build(a2: std.mem.Allocator, hits: usize) !Graph {
            const lamps = try a2.alloc(Lamp, 2);
            lamps[0] = lampOf(0, false);
            lamps[1] = lampOf(0, true);
            const gates = try a2.alloc(Gate, 1);
            const params = try a2.alloc(usize, 2);
            params[0] = 0;
            params[1] = 1;
            const gate_wires = try a2.alloc(usize, 1);
            gate_wires[0] = hits;
            gates[0] = try gateOf(a2, .fault_n, lamps, params, gate_wires, false);
            const wires = try a2.alloc(Wire, hits + 1);
            for (0..hits) |i| {
                const targets = try a2.alloc(Target, 1);
                targets[0] = .{ .fault_gate = 0 };
                wires[i] = .{ .targets = targets };
            }
            const out_targets = try a2.alloc(Target, 1);
            out_targets[0] = .{ .output_port = 7 };
            wires[hits] = .{ .targets = out_targets };
            const port_wires = try a2.alloc(usize, hits);
            for (0..hits) |i| port_wires[i] = i;
            return makeGraph(a2, lamps, gates, wires, &.{.{ .port = 0, .wires = port_wires }});
        }

        fn firedCount(a2: std.mem.Allocator, hits: usize, seed_base: u64, trials: u32) !u32 {
            var fired: u32 = 0;
            for (0..trials) |i| {
                const g = try build(a2, hits);
                var sim = try Sim.init(a2, &g, seed_base + i);
                if ((try sim.event(0)).len == 1) fired += 1;
            }
            return fired;
        }
    };

    const trials: u32 = 6000;
    const fired1 = try B.firedCount(a, 1, 1000, trials);
    try testing.expect(fired1 > 2820 and fired1 < 3180);
    const fired2 = try B.firedCount(a, 2, 2000, trials);
    try testing.expect(fired2 > 4320 and fired2 < 4680);
}

test "output fires once per wire trip" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 1);
    lamps[0] = lampOf(0, false);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_1, lamps, &.{0}, &.{1}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{5} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{5} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
    });
    try testing.expectEqualSlices(i32, &.{ 5, 5 }, try run(a, &g, 0));
}

test "feedback loop terminates and fires once per trip" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    // Gate 0's wire 0 feeds back into its own lamp and output 7.
    const lamps = try a.alloc(Lamp, 1);
    lamps[0] = lampOf(0, false);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_1, lamps, &.{0}, &.{0}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
    });
    try testing.expectEqualSlices(i32, &.{ 7, 7 }, try run(a, &g, 0));
}

test "double occurrence expands to two events" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const lamps = try a.alloc(Lamp, 1);
    lamps[0] = lampOf(0, false);
    const gates = try a.alloc(Gate, 1);
    gates[0] = try gateOf(a, .gate_1, lamps, &.{0}, &.{1}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{7} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
    });
    var sim = try Sim.init(a, &g, 1);
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(0));
    try testing.expectEqualSlices(i32, &.{7}, try sim.event(0));
}

test "gate marked before later trips still pulses" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    // G0 (lamp 0) and G1 (lamp 1) both dirty from one input event; G0's
    // output wire 2 also carries lamp 1, toggling it back off. G1 must still
    // pulse because it was marked during the check phase.
    const lamps = try a.alloc(Lamp, 2);
    lamps[0] = lampOf(0, false);
    lamps[1] = lampOf(1, false);
    const gates = try a.alloc(Gate, 2);
    gates[0] = try gateOf(a, .gate_1, lamps, &.{0}, &.{2}, false);
    gates[1] = try gateOf(a, .gate_1, lamps, &.{1}, &.{3}, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{0}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{1}, .fault_gates = &.{}, .outputs = &.{} },
        .{ .lamps = &.{1}, .fault_gates = &.{}, .outputs = &.{8} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{9} },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{ 0, 1 } },
    });
    try testing.expectEqualSlices(i32, &.{ 8, 9 }, try run(a, &g, 0));
}

test "arena size stabilizes under repeated events" {
    // The sim's only per-event allocation is the output buffer, which grows
    // monotonically up to the world's maximum per-event fanout and then
    // stays put. The world arena must not grow with event count.
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    const lamps = try a.alloc(Lamp, 0);
    const gates = try a.alloc(Gate, 0);
    var outs: [200]i32 = undefined;
    for (&outs, 0..) |*o, i| o.* = @intCast(i);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &outs },
    });
    const g = try makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
    });

    var sim = try Sim.init(a, &g, 1);

    // Warmup: any one-time growth (output buffer reaching the world's max
    // fanout) happens here.
    for (0..10_000) |_| _ = try sim.event(0);
    const cap_after_warmup = arena.queryCapacity();

    for (0..100_000) |_| _ = try sim.event(0);
    try testing.expectEqual(cap_after_warmup, arena.queryCapacity()); // never grows again

    // A burst big enough to exhaust the arena node slack still grows only
    // once, then stays flat.
    var big_outs: [10_000]i32 = undefined;
    for (&big_outs, 0..) |*o, i| o.* = @intCast(i);
    const big_wires = try makeWires(a, &.{
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &big_outs },
    });
    const big_g = try makeGraph(a, lamps, gates, big_wires, &.{
        .{ .port = 0, .wires = &.{0} },
    });
    var big_sim = try Sim.init(a, &big_g, 1);
    _ = try big_sim.event(0); // grows out to 10_000 entries, past the slack
    const cap_after_burst = arena.queryCapacity();
    try testing.expect(cap_after_burst > cap_after_warmup);

    for (0..10_000) |_| _ = try big_sim.event(0);
    try testing.expectEqual(cap_after_burst, arena.queryCapacity());
}

// Real RGB hot path: repeated control wires, deterministic fault trigger masks,
// output duplicates and a probabilistic fallback, all sharing a wave.
fn regionFixture(a: std.mem.Allocator) !Graph {
    const lamps = try a.alloc(Lamp, 11);
    const gates = try a.alloc(Gate, 10);
    for (0..8) |i| {
        lamps[i] = lampOf(@intCast(i), i % 2 == 0);
        const ids = try a.alloc(usize, 1);
        ids[0] = i;
        gates[i] = try gateOf(a, .fault_1, lamps, ids, &.{3}, false);
    }
    lamps[8] = lampOf(8, false);
    lamps[9] = lampOf(8, true);
    gates[8] = try gateOf(a, .fault_n, lamps, &.{ 8, 9 }, &.{3}, false);
    lamps[10] = lampOf(9, false);
    gates[9] = try gateOf(a, .gate_1, lamps, &.{10}, &.{ 0, 1, 3, 4 }, false);
    const wires = try a.alloc(Wire, 6);
    const ctrl = try a.alloc(Target, 9);
    for (0..9) |i| ctrl[i] = .{ .lamp_on_fault = i };
    wires[0] = .{ .targets = ctrl };
    const faults = try a.alloc(Target, 10);
    for (0..8) |i| faults[i] = .{ .fault_gate = i };
    faults[8] = .{ .output_port = 6 };
    faults[9] = .{ .output_port = 6 };
    wires[1] = .{ .targets = faults };
    wires[2] = .{ .targets = &.{ .{ .fault_gate = 8 }, .{ .fault_gate = 8 } } };
    wires[3] = .{ .targets = &.{ .{ .output_port = 1 }, .{ .output_port = 2 }, .{ .output_port = 2 } } };
    wires[4] = .{ .targets = &.{.{ .lamp = 10 }} };
    // A mixed deterministic/probabilistic run must stay uncompressed.
    const mixed = try a.alloc(Target, 9);
    for (0..9) |i| mixed[i] = .{ .fault_gate = i };
    wires[5] = .{ .targets = mixed };
    return makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{ 0, 0, 1, 2, 4 } },
        .{ .port = 1, .wires = &.{ 0, 1, 1, 2, 5 } },
        .{ .port = 2, .wires = &.{ 1, 3, 5 } },
    });
}

test "v3 wave-local regions preserve state, duplicates, checks and RNG across mixed continuation" {
    var arena = std.heap.ArenaAllocator.init(testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g0 = try regionFixture(a);
    const g1 = try regionFixture(a);
    var base = try Sim.init(a, &g0, 0xC0FFEE);
    var fast = try Sim.init(a, &g1, 0xC0FFEE);
    fast.regions = try macha.Regions.init(a, &g1);
    try testing.expectEqual(@as(usize, 1), fast.regions.?.region_count);
    try testing.expectEqual(@as(usize, 1), fast.regions.?.control_count);
    try testing.expectEqual(@as(usize, 2), fast.regions.?.output_count);
    var rng = std.Random.DefaultPrng.init(42);
    for (0..2000) |i| {
        // Switch back and forth at event boundaries; no stale dirties.
        fast.regions_enabled = i % 7 != 0;
        const port: i32 = @intCast(rng.random().uintLessThan(u32, 3));
        const x = try base.event(port);
        const y = try fast.event(port);
        try testing.expectEqualSlices(i32, x, y);
        for (g0.lamps, g1.lamps) |l, r| try testing.expectEqual(l.on, r.on);
        try testing.expectEqualSlices(bool, base.gate_state, fast.gate_state);
        try testing.expectEqualSlices(u32, base.fault_hits, fast.fault_hits);
        try testing.expectEqual(base.checks, fast.checks);
        try testing.expect(std.meta.eql(base.prng.*, fast.prng.*));
        try testing.expectEqual(@as(usize, 0), fast.regions.?.control_len);
        for (fast.regions.?.words) |word| try testing.expectEqual(@as(u64, 0), word);
    }
}

fn lazyFixture(a: std.mem.Allocator, overlap: bool) !Graph {
    const g = try regionFixture(a);
    // Remove the probabilistic lamp from the long control run. The eight
    // remaining lamps have different initial values and one deferred group.
    g.wires[0].targets = g.wires[0].targets[0..8];
    if (overlap) {
        g.wires[2].targets = &.{ .{ .lamp_on_fault = 0 }, .{ .fault_gate = 8 }, .{ .fault_gate = 8 } };
    }
    return g;
}

test "v3.1 shared control parity remains exact across waves and event mode changes" {
    var arena = std.heap.ArenaAllocator.init(testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g0 = try lazyFixture(a, true);
    const g1 = try lazyFixture(a, true);
    var base = try Sim.init(a, &g0, 71);
    var fast = try Sim.init(a, &g1, 71);
    fast.regions = try macha.Regions.init(a, &g1);
    try testing.expectEqual(@as(usize, 1), fast.regions.?.lazy_count);
    try testing.expectEqual(@as(usize, 8), fast.regions.?.lazy_lamps);
    fast.profile_regions = true;
    var rng = std.Random.DefaultPrng.init(42);
    for (0..2000) |i| {
        fast.regions_enabled = i % 7 != 0;
        fast.lazy_enabled = i % 5 != 0;
        const port: i32 = @intCast(rng.random().uintLessThan(u32, 3));
        try testing.expectEqualSlices(i32, try base.event(port), try fast.event(port));
        for (g0.lamps, g1.lamps) |l, r| try testing.expectEqual(l.on, r.on);
        try testing.expectEqualSlices(bool, base.gate_state, fast.gate_state);
        try testing.expectEqualSlices(u32, base.fault_hits, fast.fault_hits);
        try testing.expectEqual(base.checks, fast.checks);
        try testing.expect(std.meta.eql(base.prng.*, fast.prng.*));
        try testing.expectEqual(@as(usize, 0), fast.regions.?.lazy_len);
        for (fast.regions.?.lazy_state) |state| try testing.expectEqual(@as(u8, 0), state);
        if (i % 113 == 0) { base.reset(); fast.reset(); }
    }
    try testing.expect(fast.regions.?.counters.lazy_wave_toggles > 0);
    try testing.expect(fast.regions.?.counters.lazy_commits > 0);
    const overlap = try lazyFixture(a, false);
    overlap.wires[2].targets = overlap.wires[0].targets;
    const rejected = try macha.Regions.init(a, &overlap);
    try testing.expectEqual(@as(usize, 0), rejected.lazy_count);
    const duplicate = try lazyFixture(a, false);
    const targets = try a.alloc(Target, 9);
    @memcpy(targets[0..8], duplicate.wires[0].targets);
    targets[8] = .{ .lamp_on_fault = 0 };
    duplicate.wires[0].targets = targets;
    const rejected_duplicate = try macha.Regions.init(a, &duplicate);
    try testing.expectEqual(@as(usize, 0), rejected_duplicate.lazy_count);
}


test "v3.2 pruned masks retain ordered fallback and lazy controls" {
    var arena = std.heap.ArenaAllocator.init(testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    for ([_]bool{ false, true }) |keep| {
        const g0 = try lazyFixture(a, true);
        const g1 = try lazyFixture(a, true);
        var base = try Sim.init(a, &g0, 32);
        var fast = try Sim.init(a, &g1, 32);
        const allow: []const usize = if (keep) &.{1} else &.{};
        fast.regions = try macha.Regions.initPolicy(a, &g1, true, 0, allow);
        try testing.expectEqual(@as(usize, if (keep) 1 else 0), fast.regions.?.region_count);
        try testing.expectEqual(@as(usize, if (keep) 0 else 1), fast.regions.?.pruned_count);
        fast.regions_enabled = true;
        fast.lazy_enabled = true;
        fast.compact_enabled = true;
        var rng = std.Random.DefaultPrng.init(32);
        for (0..500) |i| {
            fast.profile_regions = i % 2 == 0;
            const port: i32 = @intCast(rng.random().uintLessThan(u32, 3));
            try testing.expectEqualSlices(i32, try base.event(port), try fast.event(port));
            for (g0.lamps, g1.lamps) |l, r| try testing.expectEqual(l.on, r.on);
            try testing.expectEqualSlices(bool, base.gate_state, fast.gate_state);
            try testing.expectEqualSlices(u32, base.fault_hits, fast.fault_hits);
            try testing.expectEqual(base.checks, fast.checks);
            try testing.expect(std.meta.eql(base.prng.*, fast.prng.*));
            try testing.expectEqual(@as(usize, 0), fast.regions.?.lazy_len);
            for (fast.regions.?.words) |word| try testing.expectEqual(@as(u64, 0), word);
        }
        if (!keep) try testing.expect(fast.regions.?.counters.pruned_triggers > 0);
    }
}


test "v3.3 direct lazy writes and fired deterministic fault reads retain parity" {
    var arena = std.heap.ArenaAllocator.init(testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g0 = try lazyFixture(a, true);
    const g1 = try lazyFixture(a, true);
    // Add deterministic feedback: gate 0 is triggered again after emitting.
    g0.gates[0].wires = &.{ 1, 3, 4 };
    g1.gates[0].wires = &.{ 1, 3, 4 };
    var base = try Sim.init(a, &g0, 135);
    var fast = try Sim.init(a, &g1, 135);
    fast.regions = try macha.Regions.initPolicy(a, &g1, true, 0, &.{1});
    fast.regions_enabled = true;
    fast.lazy_enabled = true;
    fast.compact_enabled = true;
    fast.direct_enabled = true;
    fast.profile_regions = true;
    var rng = std.Random.DefaultPrng.init(135);
    for (0..1000) |i| {
        fast.direct_enabled = i % 7 != 0;
        fast.profile_regions = i % 3 != 0;
        const port: i32 = @intCast(rng.random().uintLessThan(u32, 3));
        try testing.expectEqualSlices(i32, try base.event(port), try fast.event(port));
        for (g0.lamps, g1.lamps) |l, r| try testing.expectEqual(l.on, r.on);
        try testing.expectEqualSlices(bool, base.gate_state, fast.gate_state);
        try testing.expectEqualSlices(u32, base.fault_hits, fast.fault_hits);
        try testing.expectEqual(base.checks, fast.checks);
        try testing.expect(std.meta.eql(base.prng.*, fast.prng.*));
        try testing.expectEqual(@as(usize, 0), fast.regions.?.control_len);
        try testing.expectEqual(@as(usize, 0), fast.regions.?.lazy_len);
        for (fast.regions.?.lazy_state) |state| try testing.expectEqual(@as(u8, 0), state);
        for (fast.regions.?.words) |word| try testing.expectEqual(@as(u64, 0), word);
        if (i % 113 == 0) { base.reset(); fast.reset(); }
    }
    try testing.expect(fast.regions.?.counters.direct_lazy_visits > 0);
    try testing.expect(fast.regions.?.counters.skipped_fault_checks > 0);
}


test "v3.5 compact short plans preserve ordered duplicates, fault RNG and fallback" {
    var arena = std.heap.ArenaAllocator.init(testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g0 = try lazyFixture(a, true);
    const g1 = try lazyFixture(a, true);
    const short: []const Target = &.{ .{ .lamp = 10 }, .{ .lamp = 10 }, .{ .lamp_on_fault = 0 }, .{ .fault_gate = 0 }, .{ .fault_gate = 0 } };
    g0.wires[4].targets = short;
    g1.wires[4].targets = short;
    var base = try Sim.init(a, &g0, 351);
    var fast = try Sim.init(a, &g1, 351);
    fast.regions = try macha.Regions.initPolicy(a, &g1, true, 0, &.{1});
    try fast.regions.?.initFastPlans(a, &g1);
    try testing.expectEqual(@as(usize, 3), fast.regions.?.fast_plan_count);
    fast.regions_enabled = true;
    fast.lazy_enabled = true;
    fast.compact_enabled = true;
    var rng = std.Random.DefaultPrng.init(351);
    for (0..1000) |i| {
        fast.fast_enabled = i % 7 != 0;
        fast.profile_regions = i % 3 != 0;
        const port: i32 = @intCast(rng.random().uintLessThan(u32, 3));
        try testing.expectEqualSlices(i32, try base.event(port), try fast.event(port));
        for (g0.lamps, g1.lamps) |l, r| try testing.expectEqual(l.on, r.on);
        try testing.expectEqualSlices(bool, base.gate_state, fast.gate_state);
        try testing.expectEqualSlices(u32, base.fault_hits, fast.fault_hits);
        try testing.expectEqual(base.checks, fast.checks);
        try testing.expect(std.meta.eql(base.prng.*, fast.prng.*));
        try testing.expectEqual(@as(usize, 0), fast.regions.?.lazy_len);
        for (fast.regions.?.words) |word| try testing.expectEqual(@as(u64, 0), word);
        if (i % 113 == 0) { base.reset(); fast.reset(); }
    }
    try testing.expect(fast.regions.?.counters.fast_wire_visits > 0);
    try testing.expect(fast.regions.?.counters.fast_short_ops > 0);
}

// Exercise the actual V4.1 scheduler even with the generic generated stub.
fn continuationFixture(a: std.mem.Allocator) !Graph {
    const lamps = try a.alloc(Lamp, 7);
    for (0..5) |i| lamps[i] = lampOf(@intCast(i), false);
    lamps[5] = lampOf(5, true);
    lamps[6] = lampOf(5, false);
    const gates = try a.alloc(Gate, 6);
    gates[0] = try gateOf(a, .gate_1, lamps, &.{0}, &.{1}, false);
    gates[1] = try gateOf(a, .gate_1, lamps, &.{1}, &.{ 2, 2 }, false);
    gates[2] = try gateOf(a, .gate_1, lamps, &.{2}, &.{ 3, 1, 3 }, false);
    gates[3] = try gateOf(a, .gate_1, lamps, &.{3}, &.{ 4, 2, 3, 2 }, false);
    gates[4] = try gateOf(a, .gate_1, lamps, &.{4}, &.{}, false);
    gates[5] = try gateOf(a, .fault_n, lamps, &.{ 5, 6 }, &.{ 3, 4 }, false);
    const wires = try makeWires(a, &.{
        .{ .lamps = &.{ 0, 1, 2, 3, 4 }, .fault_gates = &.{ 5, 5 }, .outputs = &.{ 6, 6 } },
        .{ .lamps = &.{0}, .fault_gates = &.{5}, .outputs = &.{ 7, 7 } },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{9} },
        .{ .lamps = &.{}, .fault_gates = &.{}, .outputs = &.{ 5, 5 } },
        .{ .lamps = &.{}, .fault_gates = &.{5}, .outputs = &.{8} },
        .{ .lamps = &.{4}, .fault_gates = &.{}, .outputs = &.{} },
    });
    return makeGraph(a, lamps, gates, wires, &.{
        .{ .port = 0, .wires = &.{0} },
        .{ .port = 1, .wires = &.{ 1, 0, 4 } },
        .{ .port = 2, .wires = &.{5} },
    });
}

test "V4.1 actual continuation scheduler preserves ordered feedback and RNG" {
    var arena = std.heap.ArenaAllocator.init(testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g0 = try continuationFixture(a);
    const g1 = try continuationFixture(a);
    var base = try Sim.init(a, &g0, 41);
    var opt = try Sim.init(a, &g1, 41);
    opt.regions = try macha.Regions.initPolicy(a, &g1, true, 2048, &.{});
    try opt.regions.?.initFastPlans(a, &g1);
    try opt.regions.?.initPackedGates(a, &g1);
    opt.continuations = try macha.Continuations.init(a, &g1);
    opt.regions_enabled = true;
    opt.compact_enabled = true;
    opt.fast_enabled = true;
    opt.packed_enabled = true;
    opt.continuation_enabled = true;
    opt.profile_regions = true;
    for (0..128) |i| {
        if (i == 64) { base.reset(); opt.reset(); }
        const port: i32 = @intCast(i % 3);
        try testing.expectEqualSlices(i32, try base.event(port), try opt.event(port));
        try testing.expectEqualSlices(bool, base.gate_state, opt.gate_state);
        try testing.expectEqualSlices(u32, base.fault_hits, opt.fault_hits);
        try testing.expectEqualSlices(usize, base.fired.slice(), opt.fired.slice());
        try testing.expectEqual(base.checks, opt.checks);
        try testing.expect(std.meta.eql(base.prng.*, opt.prng.*));
        for (g0.lamps, g1.lamps) |l0, l1| try testing.expectEqual(l0.on, l1.on);
        try testing.expectEqual(@as(usize, 0), opt.continuations.?.pending.items.len);
        try testing.expectEqual(@as(usize, 0), opt.dirty.len);
    }
    for (opt.continuation_counts) |n| try testing.expect(n > 0);
    try testing.expect(opt.packed_fallback_checks > 0);
}
