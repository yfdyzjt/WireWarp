const std = @import("std");
const macha = @import("macha");

pub fn main(init: std.process.Init) !void {
    const arena = init.arena.allocator();
    const args = try init.minimal.args.toSlice(arena);
    if (args.len < 2) {
        std.debug.print("usage: macha <world.wld> | macha serve [--empty] [--pipe <path>] | macha replay <world.wld> <scenario.bin> <out.bin>\n", .{});
        return;
    }
    if (std.mem.eql(u8, args[1], "serve")) {
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
                std.debug.print("usage: macha serve [--empty] [--pipe <path>]\n", .{});
                return;
            } else {
                std.debug.print("macha: unknown serve argument {s}\n", .{args[i]});
                return;
            }
        }
        macha.server.run(init.io, std.heap.page_allocator, override, empty);
        return;
    }

    if (std.mem.eql(u8, args[1], "replay")) {
        if (args.len < 5) {
            std.debug.print("usage: macha replay <world.wld> <scenario.bin> <out.bin>\n", .{});
            return;
        }
        try replay(init, arena, args[2], args[3], args[4]);
        return;
    }

    const path: []const u8 = args[1];
    const events: u64 = 100_000;

    const io = init.io;
    const wwir_path = try macha.files.withExt(arena, path, "wwir");
    const wwio_path = try macha.files.withExt(arena, path, "wwio");

    var scratch = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    const t = scratch.allocator();

    const wwir = try std.Io.Dir.cwd().readFileAlloc(io, wwir_path, t, .unlimited);
    const wwio = try std.Io.Dir.cwd().readFileAlloc(io, wwio_path, t, .unlimited);

    const wiring = try macha.files.parseWiring(t, wwir);
    const iof = try macha.files.parseIo(t, wwio);
    if (!std.mem.eql(u8, wiring.hash[0..], iof.hash[0..])) {
        std.debug.print("warning: hash mismatch between .wwir and .wwio\n", .{});
    }

    const plate_type = macha.files.InputType.projectile_pressure_pad; // the world's clock sources
    var plates_buf = try macha.util.Buf(i32).init(arena, 64);
    var others_buf = try macha.util.Buf(i32).init(arena, 64);
    for (iof.inputs) |in| {
        if (in.type_ == @intFromEnum(plate_type))
            try plates_buf.append(arena, in.port_id)
        else
            try others_buf.append(arena, in.port_id);
    }
    const plates = plates_buf.slice();
    const others = others_buf.slice();
    if (plates.len == 0 and others.len == 0) return error.NoInputs;
    std.debug.print("inputs: {d} clock pads ({}), {d} others\n", .{ plates.len, plate_type, others.len });
    var type_counts: [256]u64 = [_]u64{0} ** 256;
    for (iof.inputs) |in| type_counts[in.type_] += 1;
    for (type_counts, 0..) |count, t2| {
        if (count > 0) {
            const t8: u8 = @intCast(t2);
            if (std.enums.fromInt(macha.files.InputType, t8)) |it| {
                std.debug.print("  input type {} ({d}): {d} records\n", .{ it, t8, count });
            } else {
                std.debug.print("  input type unknown ({d}): {d} records\n", .{ t8, count });
            }
        }
    }

    const g = try macha.compiler.compile(arena, &wiring);
    scratch.deinit(); // parsed files are no longer referenced

    var sim = try macha.Sim.init(arena, &g, 0xC0FFEE);
    var rng = std.Random.DefaultPrng.init(42);
    var total_outputs: u64 = 0;

    const t0 = std.Io.Clock.now(.awake, io);
    var i: u64 = 0;
    while (i < events) : (i += 1) {
        const want_plate = rng.random().uintLessThan(u8, 100) < 99;
        const pool: []const i32 = if (want_plate and plates.len > 0)
            plates
        else if (!want_plate and others.len > 0)
            others
        else if (plates.len > 0)
            plates
        else
            others;
        const port = pool[rng.random().uintLessThan(usize, pool.len)];

        total_outputs += (try sim.event(port)).len;
    }
    const t1 = std.Io.Clock.now(.awake, io);
    const elapsed_ns: i96 = t1.nanoseconds - t0.nanoseconds;
    const elapsed_ms: i96 = @divTrunc(elapsed_ns, std.time.ns_per_ms);
    const per_event: i96 = if (events == 0) 0 else @divTrunc(elapsed_ns, @as(i96, @intCast(events)));

    std.debug.print(
        "world: {d} wires, {d} lamps, {d} gates, {d} inputs\n",
        .{ g.wires.len, g.lamps.len, g.gates.len, g.input_ports.len },
    );
    std.debug.print(
        "sim: {d} events in {d} ms ({d} ns/event), {d} checks, {d} outputs\n",
        .{ events, elapsed_ms, per_event, sim.checks, total_outputs },
    );
}

fn stateHash(g: *const macha.Graph, sim: *const macha.Sim) u64 {
    var h: u64 = 0xcbf29ce484222325;
    for (g.lamps) |lamp| {
        h ^= @as(u64, @intFromBool(lamp.on));
        h *%= 0x100000001b3;
    }
    for (g.gates, 0..) |gate, i| {
        switch (gate.class) {
            .fault_1, .fault_n => continue,
            else => {},
        }
        h ^= @as(u64, @intFromBool(sim.gate_state[i]));
        h *%= 0x100000001b3;
    }
    return h;
}

fn writeIntLe(comptime T: type, a: std.mem.Allocator, buf: *macha.util.Buf(u8), v: T) !void {
    try buf.ensureUnusedCapacity(a, @sizeOf(T));
    var bytes: [@sizeOf(T)]u8 = undefined;
    std.mem.writeInt(T, &bytes, v, .little);
    buf.appendSliceAssumeCapacity(&bytes);
}

/// Replay mode: runs a scenario produced by galvanic's differential driver
/// and writes per-event outputs + state fingerprints to `out_path`.
fn replay(
    init: std.process.Init,
    a: std.mem.Allocator,
    world_path: []const u8,
    scenario_path: []const u8,
    out_path: []const u8,
) !void {
    const io = init.io;

    const scenario = try std.Io.Dir.cwd().readFileAlloc(io, scenario_path, a, .unlimited);
    var r = macha.util.Reader.init(scenario);
    const seed = try r.readU64();
    const count = try r.readU64();
    const stride = try r.readU64(); // state hash computed every stride events
    const ports = try a.alloc(i32, @intCast(count));
    for (ports) |*p| p.* = try r.readI32();

    const wwir_path = try macha.files.withExt(a, world_path, "wwir");
    var scratch = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    const t = scratch.allocator();
    const wwir = try std.Io.Dir.cwd().readFileAlloc(io, wwir_path, t, .unlimited);
    const wiring = try macha.files.parseWiring(t, wwir);
    const g = try macha.compiler.compile(a, &wiring);
    scratch.deinit();

    var sim = try macha.Sim.init(a, &g, seed);
    var file_buf = try macha.util.Buf(u8).init(a, 4 * 1024 * 1024);

    const t0 = std.Io.Clock.now(.awake, io);
    for (ports, 0..) |port, i| {
        const outs = try sim.event(port);
        try writeIntLe(u32, a, &file_buf, @intCast(outs.len));
        for (outs) |p| try writeIntLe(i32, a, &file_buf, p);
        const hash: u64 = if (i % stride == 0) stateHash(&g, &sim) else 0;
        try writeIntLe(u64, a, &file_buf, hash);
    }
    const t1 = std.Io.Clock.now(.awake, io);

    const f = try std.Io.Dir.createFileAbsolute(io, out_path, .{ .truncate = true });
    try std.Io.File.writePositionalAll(f, io, file_buf.slice(), 0);

    const ns: i96 = t1.nanoseconds - t0.nanoseconds;
    std.debug.print("SIM_NS {d}\n", .{ns});
}
