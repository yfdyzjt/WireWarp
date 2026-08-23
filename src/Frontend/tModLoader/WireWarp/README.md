# tModLoader

The tModLoader frontend.

## Build

Requires [tModLoader](https://github.com/tModLoader/tModLoader):

```sh
dotnet build WireWarp.Frontend.tModLoader.csproj
```

## Run

1. Start the backend
2. Start tModLoader and enable the mod
3. Enter a world and wait for the backend connection

## Commands

In-game `/ww <subcommand>`:

| Subcommand | Action |
|---|---|
| `startup` | Start a session |
| `shutdown` | Close the session |
| `run` / `stop` | Start / pause frame advancement |
| `syncto` | Push frontend wiring state to the backend |
| `syncfrom` | Pull wiring state from the backend to the frontend |
| `reset` | Reset to the initial state |
| `report` | Export the wiring report file |
