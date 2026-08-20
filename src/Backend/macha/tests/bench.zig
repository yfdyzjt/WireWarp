//! Shared input-splitting helpers for the headless driver and the
//! mock-frontend benchmark.

const std = @import("std");
const macha = @import("macha");

/// Input ports split by whether they are clock sources (projectile
/// pressure pads). Backed by caller-owned allocations, so the slices stay
/// valid as long as the caller's allocator does (intended for arenas).
pub const Partition = struct {
    plates: []const i32,
    others: []const i32,

    pub fn empty(self: Partition) bool {
        return self.plates.len == 0 and self.others.len == 0;
    }

    /// Picks a port like the game's traffic: 99% clock pads, 1% others.
    pub fn pickPort(self: Partition, rng: std.Random) ?i32 {
        if (self.empty()) return null;
        const want_plate = rng.uintLessThan(u8, 100) < 99;
        const pool: []const i32 = if (want_plate and self.plates.len > 0)
            self.plates
        else if (!want_plate and self.others.len > 0)
            self.others
        else if (self.plates.len > 0)
            self.plates
        else
            self.others;
        return pool[rng.uintLessThan(usize, pool.len)];
    }
};

pub fn partitionInputs(a: std.mem.Allocator, iof: *const macha.files.IoFile) std.mem.Allocator.Error!Partition {
    var plates = try macha.util.Buf(i32).init(a, 64);
    var others = try macha.util.Buf(i32).init(a, 64);
    const plate_type = macha.files.InputType.projectile_pressure_pad;
    for (iof.inputs) |in| {
        if (in.type_ == @intFromEnum(plate_type))
            try plates.append(a, in.port_id)
        else
            try others.append(a, in.port_id);
    }
    return .{ .plates = plates.slice(), .others = others.slice() };
}

pub fn printInputTypes(iof: *const macha.files.IoFile) void {
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
}
