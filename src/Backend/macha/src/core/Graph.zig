const Graph = @This();

wires: []Wire,
lamps: []Lamp,
gates: []Gate,
input_ports: []InputPort,

pub const Wire = struct {
    targets: []const Target,
};

/// A wire trip target
pub const Target = union(enum) {
    lamp: usize,
    lamp_on_fault: usize,
    fault_gate: usize,
    output_port: i32,
};

/// Fault lamps are not lamp nodes
pub const Lamp = struct {
    gate: u32,
    on: bool,
    initial: bool,
};

pub const Gate = struct {
    class: Class,
    lamps: []*Lamp,
    wires: []const usize,
    state: bool,

    pub const Class = enum {
        fault_1,
        fault_n,
        gate_1,
        gate_2_and,
        gate_2_xor,
        gate_n_and,
        gate_n_xor,
    };
};

pub const InputPort = struct {
    wires: []const usize,
};
