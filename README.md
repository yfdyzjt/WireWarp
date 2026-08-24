# WireWarp

A high-performance gate-level acceleration platform for Terraria wiring, with separate frontend and backend.

## Architecture

| Module | Preprocessing | Runtime |
| --- | --- | --- |
| Frontend | Converts the world's wiring into an intermediate representation | Sends world inputs to the backend and applies the returned outputs to the world |
| Backend | Converts the wiring intermediate representation into a high-performance representation | Receives frontend inputs, runs the wiring simulation, and returns outputs to the frontend |
| Communication | `.wwld` world backup; `.wwio` io graph; `.wwir` wiring graph | Communicates over Named Pipe IPC |

## Frontends

| Name | Description | Status |
| --- | --- | --- |
| Shared | Shared frontend library, the common parts independent of any specific frontend | Done |
| tModLoader | [tModLoader](https://github.com/tModLoader/tModLoader) mod frontend | Done |
| TShock | [TShock](https://github.com/Pryaxis/TShock) plugin frontend | Not started |

## Backends

| Name | Description | Status |
| --- | --- | --- |
| macha | Runtime backend written in Zig | Done |
| Reference | Runtime backend written in C# | Not finished |
| Hardware | Hardware-accelerated precompiled backend | Not started |

## Build

See the README in each directory for how to build the frontends and backends.
