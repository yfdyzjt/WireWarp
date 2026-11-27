// Generic runtime only. Generated file replaces this on request.
const std = @import("std");
pub const enabled = false;
pub const fused = false;
pub const digest: [32]u8 = [_]u8{0} ** 32;
pub fn trip(self: anytype, w: usize) std.mem.Allocator.Error!bool { _ = self; _ = w; return false; }
pub fn evaluate(self: anytype, gi: usize) ?bool { _ = self; _ = gi; return null; }
pub fn event(_: anytype, _: i32) std.mem.Allocator.Error!bool { return false; }
