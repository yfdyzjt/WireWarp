const std = @import("std");
const macha = @import("macha");

const util = macha.util;
const files = macha.files;
const Graph = macha.Graph;
const compile = macha.compiler.compile;

const testing = std.testing;
const Node = files.WiringFile.Node;

fn serNodes(buf: *std.ArrayList(u8), a: std.mem.Allocator, nodes: []const Node) !void {
    var tmp: [4]u8 = undefined;
    std.mem.writeInt(i32, &tmp, @intCast(nodes.len), .little);
    try buf.appendSlice(a, &tmp);
    for (nodes) |n| {
        try buf.append(a, n.type_);
        std.mem.writeInt(i32, &tmp, n.id, .little);
        try buf.appendSlice(a, &tmp);
        std.mem.writeInt(i32, &tmp, @intCast(n.fanout.len), .little);
        try buf.appendSlice(a, &tmp);
        for (n.fanout) |f| {
            std.mem.writeInt(i32, &tmp, f, .little);
            try buf.appendSlice(a, &tmp);
        }
    }
}

fn serializeWiring(
    a: std.mem.Allocator,
    input_ports: []const Node,
    output_ports: []const Node,
    lamps: []const Node,
    gates: []const Node,
    wires: []const Node,
) ![]const u8 {
    var buf = try std.ArrayList(u8).initCapacity(a, 4096);
    var tmp: [4]u8 = undefined;
    std.mem.writeInt(u32, &tmp, util.MAGIC, .little);
    try buf.appendSlice(a, &tmp);
    std.mem.writeInt(u32, &tmp, util.FILE_VERSION, .little);
    try buf.appendSlice(a, &tmp);
    for (0..util.HASH_SIZE) |_| try buf.append(a, 0);
    std.mem.writeInt(i32, &tmp, 6, .little);
    try buf.appendSlice(a, &tmp);
    const table_pos = buf.items.len;
    for (0..6) |_| try buf.appendSlice(a, &.{ 0, 0, 0, 0 });

    var starts: [6]u32 = undefined;
    starts[0] = @intCast(buf.items.len);
    try serNodes(&buf, a, input_ports);
    starts[1] = @intCast(buf.items.len);
    try serNodes(&buf, a, output_ports);
    starts[2] = @intCast(buf.items.len);
    try serNodes(&buf, a, lamps);
    starts[3] = @intCast(buf.items.len);
    try serNodes(&buf, a, gates);
    starts[4] = @intCast(buf.items.len);
    try serNodes(&buf, a, wires);
    starts[5] = @intCast(buf.items.len);

    for (starts, 0..) |s, i| {
        const off = table_pos + i * 4;
        std.mem.writeInt(u32, buf.items[off..][0..4], s, .little);
    }
    return buf.items;
}

test "compile: classes and targets" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    const lamps = [_]Node{
        .{ .type_ = 2, .id = 1, .fanout = &.{7} }, // off
        .{ .type_ = 2, .id = 2, .fanout = &.{7} }, // off
        .{ .type_ = 1, .id = 3, .fanout = &.{8} }, // on
        .{ .type_ = 3, .id = 4, .fanout = &.{8} }, // fault
        .{ .type_ = 2, .id = 5, .fanout = &.{9} }, // off
        .{ .type_ = 2, .id = 6, .fanout = &.{9} }, // off
    };
    const gates = [_]Node{
        .{ .type_ = 1, .id = 7, .fanout = &.{10} }, // AND
        .{ .type_ = 7, .id = 8, .fanout = &.{11} }, // Fault
        .{ .type_ = 3, .id = 9, .fanout = &.{12} }, // OR
    };
    const wires = [_]Node{
        .{ .type_ = 1, .id = 10, .fanout = &.{ 1, 2 } },
        .{ .type_ = 1, .id = 11, .fanout = &.{ 3, 4 } },
        .{ .type_ = 1, .id = 12, .fanout = &.{ 5, 6 } },
    };
    const bytes = try serializeWiring(a, &.{
        .{ .type_ = 1, .id = 0, .fanout = &.{ 10, 11, 12 } },
    }, &.{}, &lamps, &gates, &wires);

    const wiring = try files.parseWiring(a, bytes);
    const g = try compile(a, &wiring);

    try testing.expectEqual(@as(usize, 6), g.lamps.len);
    try testing.expectEqual(@as(usize, 3), g.gates.len);

    try testing.expectEqual(Graph.Gate.Class.gate_2_and, g.gates[0].class);
    try testing.expectEqual(@as(usize, 2), g.gates[0].lamps.len);
    try testing.expectEqual(&g.lamps[0], g.gates[0].lamps[0]);
    try testing.expectEqual(&g.lamps[1], g.gates[0].lamps[1]);
    try testing.expectEqual(false, g.gates[0].state);

    try testing.expectEqual(Graph.Gate.Class.fault_1, g.gates[1].class);
    try testing.expectEqual(&g.lamps[2], g.gates[1].lamps[0]);
    try testing.expectEqual(true, g.lamps[2].initial);
    try testing.expectEqual(true, g.lamps[2].on);
    try testing.expectEqual(false, g.lamps[3].initial); // fault slot: always off
    try testing.expectEqual(@as(u32, 1), g.lamps[3].gate);

    try testing.expectEqual(Graph.Gate.Class.gate_2_and, g.gates[2].class);
    try testing.expectEqual(&g.lamps[4], g.gates[2].lamps[0]);
    try testing.expectEqual(&g.lamps[5], g.gates[2].lamps[1]);
    try testing.expectEqual(true, g.gates[2].state);
    try testing.expectEqual(true, g.lamps[4].initial);
    try testing.expectEqual(true, g.lamps[5].initial);

    try testing.expectEqualSlices(Graph.Target, &.{ .{ .lamp = 0 }, .{ .lamp = 1 } }, g.wires[0].targets);
    try testing.expectEqualSlices(Graph.Target, &.{ .{ .lamp_on_fault = 2 }, .{ .fault_gate = 1 } }, g.wires[1].targets);
    try testing.expectEqualSlices(Graph.Target, &.{ .{ .lamp = 4 }, .{ .lamp = 5 } }, g.wires[2].targets);

    try testing.expectEqualSlices(usize, &.{ 0, 1, 2 }, g.input_ports[0].wires);
}

test "compile: xor and nand fold" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    const lamps = [_]Node{
        .{ .type_ = 2, .id = 1, .fanout = &.{6} }, // off → AND gate
        .{ .type_ = 2, .id = 2, .fanout = &.{6} }, // off
        .{ .type_ = 2, .id = 3, .fanout = &.{7} }, // off → XOR gate
        .{ .type_ = 2, .id = 4, .fanout = &.{7} }, // off
        .{ .type_ = 2, .id = 5, .fanout = &.{8} }, // off → NAND gate
    };
    const gates = [_]Node{
        .{ .type_ = 1, .id = 6, .fanout = &.{9} }, // AND
        .{ .type_ = 5, .id = 7, .fanout = &.{10} }, // XOR
        .{ .type_ = 2, .id = 8, .fanout = &.{11} }, // NAND (single lamp)
    };
    const wires = [_]Node{
        .{ .type_ = 1, .id = 9, .fanout = &.{ 1, 2 } },
        .{ .type_ = 1, .id = 10, .fanout = &.{ 3, 4 } },
        .{ .type_ = 1, .id = 11, .fanout = &.{5} },
    };
    const bytes = try serializeWiring(a, &.{
        .{ .type_ = 1, .id = 0, .fanout = &.{ 9, 10, 11 } },
    }, &.{}, &lamps, &gates, &wires);

    const wiring = try files.parseWiring(a, bytes);
    const g = try compile(a, &wiring);

    try testing.expectEqual(Graph.Gate.Class.gate_2_and, g.gates[0].class);
    try testing.expectEqual(false, g.gates[0].state);

    try testing.expectEqual(Graph.Gate.Class.gate_2_xor, g.gates[1].class);
    try testing.expectEqual(false, g.gates[1].state);

    try testing.expectEqual(Graph.Gate.Class.gate_1, g.gates[2].class);
    try testing.expectEqual(&g.lamps[4], g.gates[2].lamps[0]);
    try testing.expectEqual(false, g.gates[2].state);
}

test "compile: fault gate without parameter lamp rejected" {
    var arena = std.heap.ArenaAllocator.init(std.testing.allocator);
    defer arena.deinit();
    const a = arena.allocator();

    // One fault lamp attached to a Fault gate, no regular lamps: the sim's
    // compound roll would divide by zero, so compile must reject it.
    const lamps = [_]Node{
        .{ .type_ = 3, .id = 0, .fanout = &.{1} }, // fault lamp → gate 1
    };
    const gates = [_]Node{
        .{ .type_ = 7, .id = 1, .fanout = &.{2} }, // Fault gate → wire 2
    };
    const wires = [_]Node{
        .{ .type_ = 1, .id = 2, .fanout = &.{0} }, // wire → lamp 0
    };
    const bytes = try serializeWiring(a, &.{}, &.{}, &lamps, &gates, &wires);
    const wiring = try files.parseWiring(a, bytes);
    try testing.expectError(error.FaultGateWithoutLamp, compile(a, &wiring));
}
