//! Compare the exact same input trace on generic and AOT paths of macha.
//! The parity pass is untimed; each timed run starts from a fresh graph/seed.
const std = @import("std");
const macha = @import("macha");
const bench = @import("bench.zig");
const seed: u64 = 0xC0FFEE;

pub fn main(init: std.process.Init) !void {
    const a = init.arena.allocator();
    const io = init.io;
    const raw_args = try init.minimal.args.toSlice(a);
    const profiling = raw_args.len > 1 and std.mem.eql(u8, raw_args[raw_args.len - 1], "--profile");
    const args = if (profiling) raw_args[0..raw_args.len - 1] else raw_args;
    if (args.len < 2 or args.len > 5) {
        std.debug.print("usage: aot-bench <world.wwld> [events=100000] [rounds=5] [input-port] [--profile]\n", .{});
        return error.BadArgs;
    }
    const events: usize = if (args.len >= 3) try std.fmt.parseInt(usize, args[2], 10) else 100_000;
    const rounds: usize = if (args.len >= 4) try std.fmt.parseInt(usize, args[3], 10) else 5;
    if (events == 0 or rounds == 0 or rounds > 100 or events > 10_000_000) return error.BadArgs;
    const wwir = try std.Io.Dir.cwd().readFileAlloc(io, try macha.files.withExt(a, args[1], "wwir"), a, .unlimited);
    const wwio = try std.Io.Dir.cwd().readFileAlloc(io, try macha.files.withExt(a, args[1], "wwio"), a, .unlimited);
    const wiring = try macha.files.parseWiring(a, wwir);
    const iof = try macha.files.parseIo(a, wwio);
    if (!std.mem.eql(u8, &wiring.hash, &iof.hash)) return error.HashMismatch;
    const trace = try a.alloc(i32, events);
    if (args.len == 5) {
        const port = try std.fmt.parseInt(i32, args[4], 10);
        if (port < 0 or port >= @as(i32, @intCast(wiring.input_ports.len))) return error.InvalidInput;
        @memset(trace, port);
        std.debug.print("TRACE: fixed input port {d} (repeated pulses)\n", .{port});
    } else {
        const pool = try bench.partitionInputs(a, &iof);
        if (pool.empty()) return error.NoInputs;
        var rng = std.Random.DefaultPrng.init(42);
        for (trace) |*port| {
            port.* = pool.pickPort(rng.random()).?;
            if (port.* < 0 or port.* >= @as(i32, @intCast(wiring.input_ports.len))) return error.InvalidInput;
        }
    }
    if (profiling) {
        if (!macha.Sim.compact_mode or !macha.Sim.direct_mode) return error.ProfileRequiresV33OrV34;
        // First verify the instrumented path per event. Then measure it in
        // isolation so reference comparisons do not pollute the timed phases.
        try verify(&wiring, wwir, trace, true, true, true, false, false, false, false, io);
        try verify(&wiring, wwir, trace, true, true, true, true, false, false, false, io);
        try profileTrace(io, &wiring, wwir, trace, 4);
        try profileTrace(io, &wiring, wwir, trace, 5);
        if (macha.Sim.fast_mode) {
            try verify(&wiring, wwir, trace, true, true, true, false, true, false, false, io);
            try profileTrace(io, &wiring, wwir, trace, 6);
        }
        if (macha.Sim.packed_mode) {
            try verify(&wiring, wwir, trace, true, true, true, false, true, true, false, io);
            try profileTrace(io, &wiring, wwir, trace, 7);
        }
        if (macha.Sim.continuation_mode) {
            try verify(&wiring, wwir, trace, true, true, true, false, true, true, true, io);
            try profileTrace(io, &wiring, wwir, trace, 8);
        }
        return;
    }
    // Compare exact ordered output IDs after every event, not just counts.
    // Compare semantic internal state after each event (fault-gate state is unused).
    if (macha.Sim.region_mode) try verify(&wiring, wwir, trace, false, false, false, false, false, false, false, null);
    try verify(&wiring, wwir, trace, macha.Sim.region_mode, false, false, false, false, false, false, null);
    if (macha.Sim.region_mode) try verify(&wiring, wwir, trace, true, true, false, false, false, false, false, null);
    if (macha.Sim.compact_mode) try verify(&wiring, wwir, trace, true, true, true, false, false, false, false, null);
    if (macha.Sim.direct_mode) try verify(&wiring, wwir, trace, true, true, true, true, false, false, false, null);
    // Warm both paths; separate measured rounds start from clean state.
    const warm = trace[0..@min(trace.len, 10_000)];
    if (macha.Sim.fast_mode) try verify(&wiring, wwir, trace, true, true, true, false, true, false, false, null);
    if (macha.Sim.packed_mode) try verify(&wiring, wwir, trace, true, true, true, false, true, true, false, null);
    if (macha.Sim.continuation_mode) try verify(&wiring, wwir, trace, true, true, true, false, true, true, true, null);
    const modes: usize = if (macha.Sim.continuation_mode) 9 else if (macha.Sim.packed_mode) 8 else if (macha.Sim.fast_mode) 7 else if (macha.Sim.direct_mode) 6 else if (macha.Sim.compact_mode) 5 else if (macha.Sim.region_mode) 4 else 2;
    for (0..modes) |mode| _ = try measure(io, &wiring, wwir, warm, mode);
    std.debug.print("mode,round,events,elapsed_ns,ns_per_event,outputs,checks\n", .{});
    for (0..rounds) |r| {
        for (0..modes) |j| {
            const mode = (r + j) % modes;
            const result = try measure(io, &wiring, wwir, trace, mode);
            std.debug.print("{s},{d},{d},{d},{d},{d},{d}\n", .{ if (mode == 0) "generic" else if (mode == 8) "v4.1" else if (mode == 7) "v4.0" else if (mode == 6) "v3.5" else if (mode == 5) "v3.3" else if (mode == 4) "v3.2" else if (mode == 3) "v3.1" else if (mode == 2) "v3" else if (macha.Sim.region_mode) "v2" else "aot", r+1, events, result.ns, @divTrunc(result.ns, @as(i96, @intCast(events))), result.outputs, result.checks });
        }
    }
}

const PortStats = struct {
    events: u64 = 0,
    fused: u64 = 0,
    outputs: u64 = 0,
    checks: u64 = 0,
    regions: macha.Regions.Counters = .{},
};

fn verify(wiring: *const macha.files.WiringFile, bytes: []const u8, trace: []const i32, use_regions: bool, lazy: bool, compact: bool, direct: bool, fast_wires: bool, packed_eval: bool, continuation: bool, stage_io: ?std.Io) !void {
    var arena = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g0 = try macha.compiler.compile(a, wiring);
    const g1 = try macha.compiler.compile(a, wiring);
    var base = try macha.Sim.init(a, &g0, seed);
    var fast = try macha.Sim.init(a, &g1, seed);
    if (!(if (continuation) fast.enableAotV41(bytes) else if (packed_eval) fast.enableAotV40(bytes) else if (fast_wires) fast.enableAotV35(bytes) else if (direct) fast.enableAotV33(bytes) else if (compact) fast.enableAotV32(bytes) else fast.enableAotVariant(bytes, use_regions, lazy))) return error.NoMatchingAotGraph;
    // Prove that the byte-level cache guard rejects altered file content.
    const altered = try a.dupe(u8, bytes);
    altered[8] ^= 1;
    if (if (continuation) fast.enableAotV41(altered) else if (packed_eval) fast.enableAotV40(altered) else if (fast_wires) fast.enableAotV35(altered) else if (direct) fast.enableAotV33(altered) else if (compact) fast.enableAotV32(altered) else fast.enableAotVariant(altered, use_regions, lazy)) return error.AotHashGuardFailed;
    if (!(if (continuation) fast.enableAotV41(bytes) else if (packed_eval) fast.enableAotV40(bytes) else if (fast_wires) fast.enableAotV35(bytes) else if (direct) fast.enableAotV33(bytes) else if (compact) fast.enableAotV32(bytes) else fast.enableAotVariant(bytes, use_regions, lazy))) return error.NoMatchingAotGraph;
    if (fast.regions) |r| std.debug.print("V3 REGIONS: {d} wires, {d} trigger entries, {d} mask blocks; {d} control runs; {d} output runs\n", .{ r.region_count, r.trigger_count, r.block_count, r.control_count, r.output_count });
    if (fast.regions) |r| std.debug.print("LAZY CONTROL: {d} eligible wires, {d} lamps; enabled={any}, region scheduler={any}\n", .{ r.lazy_count, r.lazy_lamps, fast.lazy_enabled, fast.regions_enabled });
    if (compact) {
        if (fast.regions) |r| std.debug.print("V3.2 PRUNED: {d} wires, {d} entries, {d} blocks, {d} bytes of mask storage avoided\n", .{ r.pruned_count, r.pruned_entries, r.pruned_blocks, r.pruned_blocks * macha.Regions.block_bytes });
    }
    if (fast_wires) {
        if (fast.regions) |r| std.debug.print("V3.5 FAST PLANS: {d}/{d} wires eligible; scheduler={any}\n", .{ r.fast_plan_count, r.programs.len, fast.fast_enabled });
    }
    if (packed_eval) {
        const table = fast.regions.?.packed_gates.?;
        std.debug.print("V4.0 PACKED PLANS: {d} gates; {d} bytes; counts fallback/normal1/fault1/lazy_fault1/and2/xor2: {any}\n", .{ table.plans.len, table.plans.len * @sizeOf(@TypeOf(table.plans[0])), table.counts });
    }
    if (continuation) {
        const c = fast.continuations.?;
        std.debug.print("V4.1 CONTINUATION PLANS: {d} gates; {d} bytes descriptors; {d} bytes queue capacity; counts fallback/one/two/three: {any}; scheduler={any}\n", .{ c.plans.len, c.plans.len * @sizeOf(@TypeOf(c.plans[0])), c.pending.capacity * @sizeOf(@TypeOf(c.plans[0])), c.counts, fast.regions_enabled });
    }
    fast.profile_regions = use_regions and stage_io == null;
    var stage: macha.Sim.StageProfile = undefined;
    if (stage_io) |io| stage = .{ .io = io };
    const stats = try a.alloc(PortStats, wiring.input_ports.len);
    @memset(stats, PortStats{});
    for (trace, 0..) |port, i| {
        const prior_checks = fast.checks;
        const prior_regions = if (fast.regions) |r| r.counters else macha.Regions.Counters{};
        const prior_fused = fast.fused_events;
        const prior_continuations = fast.continuation_counts;
        const x = try base.event(port);
        const y = if (stage_io != null) try fast.eventProfiled(port, &stage) else try fast.event(port);
        if (!std.mem.eql(i32, x, y)) {
            std.debug.print("Mismatch at event {d}, input {d}: generic {any}, AOT {any}\n", .{ i, port, x, y });
            return error.OutputMismatch;
        }
        for (g0.lamps, g1.lamps) |l0, l1| if (l0.on != l1.on) return error.LampMismatch;
        for (g0.gates, 0..) |gate, gi| {
            if (gate.class == .fault_1 or gate.class == .fault_n) continue;
            if (base.gate_state[gi] != fast.gate_state[gi]) return error.GateMismatch;
        }
        if (base.checks != fast.checks) return error.CheckCountMismatch;
        if (!std.meta.eql(base.prng.*, fast.prng.*)) return error.RngMismatch;
        if (!std.mem.eql(u32, base.fault_hits, fast.fault_hits)) return error.FaultHitMismatch;
        if (fast.continuations) |c| {
            if (c.pending.items.len != 0) return error.PendingContinuationMismatch;
            if (fast.regions_enabled and fast.fused_events == prior_fused) {
                if (!std.mem.eql(usize, base.fired.slice(), fast.fired.slice())) return error.FiredOrderMismatch;
                if (fast.profile_regions) {
                    var scheduled: u64 = 0;
                    for (fast.continuation_counts, prior_continuations) |now, prior| scheduled += now - prior;
                    if (scheduled != fast.fired.len) return error.ContinuationCoverageMismatch;
                }
            }
        }
        if (fast.dirty.len != 0 or fast.target_groups.items.len != 0) return error.PendingMismatch;
        if (fast.regions) |r| {
            if (r.control_len != 0 or r.lazy_len != 0) return error.PendingControlMismatch;
            for (r.lazy_state) |state| if (state != 0) return error.PendingLazyMismatch;
            for (r.words) |word| if (word != 0) return error.DirtyMaskMismatch;
        }
        const p = &stats[@intCast(port)];
        p.events += 1;
        p.fused += fast.fused_events - prior_fused;
        p.outputs += @intCast(x.len);
        p.checks += fast.checks - prior_checks;
        if (fast.regions) |r| {
            inline for (@typeInfo(macha.Regions.Counters).@"struct".fields) |field| {
                @field(p.regions, field.name) += @field(r.counters, field.name) - @field(prior_regions, field.name);
            }
        }
    }
    if (packed_eval and stage_io == null) {
        std.debug.print("{s} GATE COVERAGE: {d} packed evaluations, {d} fallback evaluations, {d} total checks (includes fused roots)\n", .{ if (continuation) "V4.1" else "V4.0", fast.packed_checks, fast.packed_fallback_checks, fast.checks });
        if (fast.packed_checks + fast.packed_fallback_checks > fast.checks) return error.PackedCoverageMismatch;
    }
    if (continuation and stage_io == null) {
        std.debug.print("V4.1 CONTINUATION COVERAGE: fallback/one/two/three {any}\n", .{fast.continuation_counts});
        // Count all scheduled fired gates for events using the continuation path.
        if (fast.continuation_counts[0] + fast.continuation_counts[1] + fast.continuation_counts[2] + fast.continuation_counts[3] > fast.checks) return error.ContinuationCoverageMismatch;
    }
    std.debug.print("PARITY MODE: {s}\n", .{if (continuation) "v4.1" else if (packed_eval) "v4.0" else if (fast_wires) "v3.5" else if (direct) "v3.3" else if (compact) "v3.2" else if (lazy) "v3.1" else if (use_regions) "v3" else "v2/node"});
    std.debug.print("PARITY PASS: {d} ordered event outputs, per-event lamps/gates, RNG, pending fault hits and gate-check counts; SHA-256 guard passed.\n", .{trace.len});
    std.debug.print("FUSION COVERAGE: {d}/{d} events used fused plans.\n", .{fast.fused_events, trace.len});
    std.debug.print("PER-PORT WORKLOAD (untimed parity trace)\nport,events,fused,outputs,checks\n", .{});
    for (stats, 0..) |p, port| {
        std.debug.print("{d},{d},{d},{d},{d}\n", .{ port, p.events, p.fused, p.outputs, p.checks });
    }
    if (use_regions and stage_io == null) {
        std.debug.print("V3 PER-PORT REGIONS (untimed)\nport,region_events,waves,wire_visits,targets,control_original,control_executed,masked_triggers,mask_blocks,batched_outputs,lazy_wave_toggles,lazy_commits,pruned_visits,pruned_triggers,direct_lazy_visits,skipped_fault_checks,fast_wire_visits,fast_short_ops\n", .{});
        for (stats, 0..) |p, port| {
            const r = p.regions;
            std.debug.print("{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d}\n", .{ port, r.region_events, r.waves, r.wire_visits, r.targets, r.control_original, r.control_executed, r.masked_triggers, r.mask_blocks, r.batched_outputs, r.lazy_wave_toggles, r.lazy_commits, r.pruned_visits, r.pruned_triggers, r.direct_lazy_visits, r.skipped_fault_checks, r.fast_wire_visits, r.fast_short_ops });
        }
    }
    if (packed_eval) {
        base.reset();
        fast.reset();
        for (trace[0..@min(trace.len, 32)]) |port| {
            const x = try base.event(port);
            const y = try fast.event(port);
            if (!std.mem.eql(i32, x, y)) return error.ResetOutputMismatch;
            for (g0.lamps, g1.lamps) |l0, l1| if (l0.on != l1.on) return error.ResetLampMismatch;
            if (!std.mem.eql(bool, base.gate_state, fast.gate_state)) return error.ResetGateMismatch;
            if (!std.mem.eql(u32, base.fault_hits, fast.fault_hits)) return error.ResetFaultMismatch;
            if (base.checks != fast.checks or !std.meta.eql(base.prng.*, fast.prng.*)) return error.ResetCounterMismatch;
        }
        std.debug.print("{s} RESET PASS\n", .{if (continuation) "V4.1" else "V4.0"});
    }
}

const Result = struct { ns: i96, outputs: u64, checks: u64 };
fn measure(io: std.Io, wiring: *const macha.files.WiringFile, bytes: []const u8, trace: []const i32, mode: usize) !Result {
    var arena = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const graph = try macha.compiler.compile(a, wiring);
    var sim = try macha.Sim.init(a, &graph, seed);
    if (mode != 0 and !(if (mode == 8) sim.enableAotV41(bytes) else if (mode == 7) sim.enableAotV40(bytes) else if (mode == 6) sim.enableAotV35(bytes) else if (mode == 5) sim.enableAotV33(bytes) else if (mode == 4) sim.enableAotV32(bytes) else sim.enableAotVariant(bytes, mode >= 2, mode == 3))) return error.NoMatchingAotGraph;
    var outputs: u64 = 0;
    const start = std.Io.Clock.now(.awake, io);
    for (trace) |port| outputs += (try sim.event(port)).len;
    const end = std.Io.Clock.now(.awake, io);
    return .{ .ns = end.nanoseconds - start.nanoseconds, .outputs = outputs, .checks = sim.checks };
}

// One fresh arena per profiled run; the comparison pass above is separate.
fn profileTrace(io: std.Io, wiring: *const macha.files.WiringFile, bytes: []const u8, trace: []const i32, mode: usize) !void {
    const baseline = try measure(io, wiring, bytes, trace, mode);
    var arena = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    defer arena.deinit();
    const a = arena.allocator();
    const g = try macha.compiler.compile(a, wiring);
    var sim = try macha.Sim.init(a, &g, seed);
    if (!(if (mode == 8) sim.enableAotV41(bytes) else if (mode == 7) sim.enableAotV40(bytes) else if (mode == 6) sim.enableAotV35(bytes) else if (mode == 5) sim.enableAotV33(bytes) else sim.enableAotV32(bytes))) return error.NoMatchingAotGraph;
    const ports = try a.alloc(macha.Sim.StageProfile, wiring.input_ports.len);
    for (ports) |*p| p.* = .{ .io = io };
    var outputs: u64 = 0;
    const start = std.Io.Clock.now(.awake, io);
    for (trace) |port| outputs += (try sim.eventProfiled(port, &ports[@intCast(port)])).len;
    const elapsed = std.Io.Clock.now(.awake, io).nanoseconds - start.nanoseconds;
    if (outputs != baseline.outputs or sim.checks != baseline.checks) return error.ProfileWorkloadMismatch;
    var totals: macha.Sim.StageProfile = .{ .io = io };
    for (ports) |p| {
        totals.events += p.events;
        totals.waves += p.waves;
        totals.fused += p.fused;
        totals.reads += p.reads;
        totals.max_event_ns = @max(totals.max_event_ns, p.max_event_ns);
        for (&totals.ns, p.ns) |*sum, n| sum.* += n;
    }
    // Calibration is a clock-pair cost estimate, not a correction applied to
    // the stage samples. Instrumentation changes codegen and cache behavior.
    var clock_pairs_ns: i96 = 0;
    for (0..10000) |_| {
        const t0 = std.Io.Clock.now(.awake, io).nanoseconds;
        const t1 = std.Io.Clock.now(.awake, io).nanoseconds;
        clock_pairs_ns += t1 - t0;
    }
    const name = if (mode == 8) "v4.1" else if (mode == 7) "v4.0" else if (mode == 6) "v3.5" else if (mode == 5) "v3.3" else "v3.2";
    std.debug.print("STAGE PROFILE: {s}; raw nanoseconds; separate run; clock overhead not subtracted\n", .{name});
    std.debug.print("profile_meta,{s},{d},{d},{d},{d},{d},{d},{d},{d}\n", .{ name, trace.len, outputs, sim.checks, baseline.ns, elapsed, totals.reads, clock_pairs_ns, totals.max_event_ns });
    std.debug.print("stage,mode,port,events,waves,fused,clock_reads,clear_ns,dispatch_ns,wire_ns,flush_ns,gates_ns,commit_ns,max_event_ns\n", .{});
    printStages(name, "all", totals);
    for (ports, 0..) |p, i| {
        if (p.events == 0) continue;
        var buf: [32]u8 = undefined;
        const label = try std.fmt.bufPrint(&buf, "{d}", .{i});
        printStages(name, label, p);
    }
    std.debug.print("STAGE PROFILE PASS: {s}\n", .{name});
}

fn printStages(name: []const u8, port: []const u8, p: macha.Sim.StageProfile) void {
    std.debug.print("stage,{s},{s},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d},{d}\n", .{ name, port, p.events, p.waves, p.fused, p.reads, p.ns[0], p.ns[1], p.ns[2], p.ns[3], p.ns[4], p.ns[5], p.max_event_ns });
}
