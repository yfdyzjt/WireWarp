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
const Buf = macha.util.Buf;

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
    var all = try Buf(i32).init(a, 16);
    for ([_]i32{ 0, 1, 1, 0, 1 }) |port| {
        try all.appendSlice(a, try sim.event(port));
    }
    try testing.expectEqualSlices(i32, &.{ 7, 7, 7 }, all.slice());
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
