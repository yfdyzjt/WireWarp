const std = @import("std");

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});
    const strip = b.option(bool, "strip", "Strip debug symbols") orelse false;

    const mod = b.addModule("macha", .{
        .root_source_file = b.path("src/root.zig"),
        .target = target,
    });
    const exe = b.addExecutable(.{
        .name = "macha",
        .root_module = b.createModule(.{
            .root_source_file = b.path("src/main.zig"),
            .target = target,
            .optimize = optimize,
            .strip = strip,
            .imports = &.{.{ .name = "macha", .module = mod }},
        }),
    });
    b.installArtifact(exe);

    const run_step = b.step("run", "Run the app");
    const run_cmd = b.addRunArtifact(exe);
    run_cmd.step.dependOn(b.getInstallStep());
    run_step.dependOn(&run_cmd.step);
    if (b.args) |args| run_cmd.addArgs(args);

    const mod_tests = b.addTest(.{ .root_module = mod });
    const exe_tests = b.addTest(.{ .root_module = exe.root_module });
    const unit_module = b.createModule(.{
        .root_source_file = b.path("tests/unit.zig"),
        .target = target,
        .optimize = optimize,
        .imports = &.{.{ .name = "macha", .module = mod }},
    });
    const unit_tests = b.addTest(.{ .root_module = unit_module });
    const test_step = b.step("test", "Run tests");
    test_step.dependOn(&b.addRunArtifact(mod_tests).step);
    test_step.dependOn(&b.addRunArtifact(exe_tests).step);
    test_step.dependOn(&b.addRunArtifact(unit_tests).step);

    // The integration suite starts `macha serve` and acts as its frontend.
    const it_module = b.createModule(.{
        .root_source_file = b.path("tests/integration.zig"),
        .target = target,
        .optimize = optimize,
        .imports = &.{.{ .name = "macha", .module = mod }},
    });
    const run_it_tests = b.addRunArtifact(b.addTest(.{ .root_module = it_module }));
    run_it_tests.setEnvironmentVariable("MACHA_BIN", b.getInstallPath(.bin, "macha"));
    run_it_tests.step.dependOn(b.getInstallStep());
    test_step.dependOn(&run_it_tests.step);

    if (target.result.os.tag != .windows) {
        const bench_module = b.createModule(.{
            .root_source_file = b.path("tests/frontend_bench.zig"),
            .target = target,
            .optimize = optimize,
            .strip = strip,
            .imports = &.{.{ .name = "macha", .module = mod }},
        });
        const bench_exe = b.addExecutable(.{
            .name = "frontend-bench",
            .root_module = bench_module,
        });
        b.installArtifact(bench_exe);
        const run_bench = b.addRunArtifact(bench_exe);
        run_bench.setEnvironmentVariable("MACHA_BIN", b.getInstallPath(.bin, "macha"));
        run_bench.step.dependOn(b.getInstallStep());
        if (b.args) |args| run_bench.addArgs(args);
        const bench_step = b.step("frontend-bench", "Measure mock-frontend pipe round-trip latency");
        bench_step.dependOn(&run_bench.step);
    }

    const sim_bench_module = b.createModule(.{
        .root_source_file = b.path("tests/sim_bench.zig"),
        .target = target,
        .optimize = optimize,
        .strip = strip,
        .imports = &.{.{ .name = "macha", .module = mod }},
    });
    const sim_bench_exe = b.addExecutable(.{
        .name = "sim-bench",
        .root_module = sim_bench_module,
    });
    b.installArtifact(sim_bench_exe);
    const run_sim_bench = b.addRunArtifact(sim_bench_exe);
    run_sim_bench.step.dependOn(b.getInstallStep());
    if (b.args) |args| run_sim_bench.addArgs(args);
    const sim_bench_step = b.step("sim-bench", "Headless simulation benchmark");
    sim_bench_step.dependOn(&run_sim_bench.step);
}
