//! Headless simulation benchmark: loads a world and drives the simulator
//! with synthetic input traffic (99% clock pads, 1% others).

const std = @import("std");
const macha = @import("macha");
const bench = @import("bench.zig");

const usage = "usage: sim-bench <world.wwld> [events]\n";

pub fn main(init: std.process.Init) !void {
    const arena = init.arena.allocator();
    const args = try init.minimal.args.toSlice(arena);
    if (args.len < 2) {
        std.debug.print("{s}", .{usage});
        return;
    }
    const events: u64 = if (args.len > 2) std.fmt.parseInt(u64, args[2], 10) catch {
        std.debug.print("{s}", .{usage});
        return;
    } else 100_000;

    const io = init.io;
    const wwir_path = try macha.files.withExt(arena, args[1], "wwir");
    const wwio_path = try macha.files.withExt(arena, args[1], "wwio");

    var scratch = std.heap.ArenaAllocator.init(std.heap.page_allocator);
    const t = scratch.allocator();
    const wwir = try std.Io.Dir.cwd().readFileAlloc(io, wwir_path, t, .unlimited);
    const wwio = try std.Io.Dir.cwd().readFileAlloc(io, wwio_path, t, .unlimited);

    const wiring = try macha.files.parseWiring(t, wwir);
    const iof = try macha.files.parseIo(t, wwio);
    if (!std.mem.eql(u8, wiring.hash[0..], iof.hash[0..])) {
        std.debug.print("warning: hash mismatch between .wwir and .wwio\n", .{});
    }

    const pool = try bench.partitionInputs(arena, &iof);
    if (pool.empty()) return error.NoInputs;
    std.debug.print("inputs: {d} clock pads ({}), {d} others\n", .{ pool.plates.len, macha.files.InputType.projectile_pressure_pad, pool.others.len });
    bench.printInputTypes(&iof);

    const g = try macha.compiler.compile(arena, &wiring);
    scratch.deinit(); // parsed files are no longer referenced

    var sim = try macha.Sim.init(arena, &g, 0xC0FFEE);
    var rng = std.Random.DefaultPrng.init(42);
    var total_outputs: u64 = 0;

    const t0 = std.Io.Clock.now(.awake, io);
    for (0..events) |_| {
        total_outputs += (try sim.event(pool.pickPort(rng.random()).?)).len;
    }
    const t1 = std.Io.Clock.now(.awake, io);
    const elapsed_ns: i96 = t1.nanoseconds - t0.nanoseconds;
    const per_event: i96 = @divTrunc(elapsed_ns, @as(i96, @intCast(events)));

    std.debug.print(
        "world: {d} wires, {d} lamps, {d} gates, {d} inputs\n",
        .{ g.wires.len, g.lamps.len, g.gates.len, g.input_ports.len },
    );
    std.debug.print(
        "sim: {d} events in {d} ms ({d} ns/event), {d} checks, {d} outputs\n",
        .{ events, @divTrunc(elapsed_ns, std.time.ns_per_ms), per_event, sim.checks, total_outputs },
    );
}
