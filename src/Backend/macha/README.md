# macha

WireWarp's Zig backend.

## Build

```sh
zig build --release=fast
```

The binary is written to `zig-out/bin/macha`.

## Run

```sh
macha serve
```

## Bench

```sh
zig build sim-bench --release=fast -- <world.wwld> [events]
```
