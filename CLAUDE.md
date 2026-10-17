# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpenMU is a C# / .NET 10 server for the MU Online MMORPG. It is **not** based on decompiled server sources. The project supports Season 6 Episode 3 (English protocol) as its primary target and is actively being refactored to use a Protobuf + WebSocket protocol for a Godot 4 C# custom client, replacing the legacy binary MU protocol.

## Build & Run Commands

```bash
# Build entire solution
dotnet build MUnique.OpenMU.sln

# Run the server (single-process mode, uses PostgreSQL)
dotnet run --project src/Startup/MUnique.OpenMU.Startup.csproj

# Demo mode (in-memory, no database required)
dotnet run --project src/Startup/MUnique.OpenMU.Startup.csproj -- -demo

# Reinitialize database
dotnet run --project src/Startup/MUnique.OpenMU.Startup.csproj -- -reinit

# Run all tests
dotnet test MUnique.OpenMU.sln

# Run a single test project
dotnet test tests/MUnique.OpenMU.GameLogic.Tests/

# Run a single test by name
dotnet test tests/MUnique.OpenMU.GameLogic.Tests/ --filter "FullyQualifiedName~TestMethodName"
```

### Prerequisites for manual development
- PostgreSQL running locally; configure `src/Persistence/EntityFramework/ConnectionSettings.xml`
- .NET SDK 10
- NodeJS 16+ (for admin panel assets)
- Admin panel: `http://localhost/` after startup. Start connect servers and at least one game server there.

### EF Core Migrations (run in Package Manager Console)
```
Add-Migration [Name] -context EntityDataContext
```
Select `MUnique.OpenMU.Persistence.EntityFramework` as both default and startup project.

---

## Architecture

### Layer Map

```
TRANSPORT    src/Network/
             WebSocketConnection (new), Connection (legacy TCP)
             IConnection: PacketReceived, Output (PipeWriter), OutputLock

PROTOCOL     src/GameServer/MessageHandler/  — deserializes packets → calls PlayerActions
             src/GameServer/RemoteView/       — serializes packets ← called by GameLogic via IViewPlugIn

CONTRACT     src/GameLogic/Views/
             IViewPlugIn and specializations — pure interfaces, no network awareness

GAME LOGIC   src/GameLogic/
             PlayerActions, GameMap, Walker, Skills, AoI
             Zero references to Network or packet types

DATA         src/DataModel/  +  src/Persistence/
             EF Core, PostgreSQL, IPersistenceContextProvider
```

### Key Design Rules (enforced as PR blockers — see `docs/custom/08-refactoring-rules.md`)

- **P1 — GameLogic never knows the protocol.** `src/GameLogic/` has zero `using MUnique.OpenMU.Network` imports. The only outbound communication is through `IViewPlugIn` interfaces in `src/GameLogic/Views/`. Any violation is an automatic PR rejection.
- **P2 — All gameplay logic is server-authoritative.** Cooldowns, damage, positions — all computed on the server. The client sends inputs; the server replies with results.
- **P3 — Database is the only source of truth for game configuration.** No gameplay constants (damage, buff duration, drop rate) hardcoded in runtime code. Everything comes from `GameConfiguration` loaded from PostgreSQL.
- **P4 — PlugIn system is the only extension mechanism.** New game events: (1) create `IXxxPlugIn` in `src/GameLogic/Views/`, (2) implement in `src/GameServer/RemoteView/Protobuf/`, (3) tag with `[PlugIn]`.
- **P5 — Single tick loop per map instance.** All time-dependent logic (movement, cooldowns, monster AI, buff expiry, snapshot broadcast) runs inside `GameTickLoop`. Per-entity timers (`new Timer(...)`, `Task.Delay` in walkers/AI) are forbidden in new code.
- **P6 — Legacy protocol compatibility is irrelevant.** The original MU binary client is discarded. Do not add, maintain, or test code solely for the legacy binary protocol.
- **P8 — Soft-delete is mandatory for player entities.** Characters use `DeletedAt DateTime?`; items use `IsDeleted bool`. Hard-delete requires a scheduled maintenance job.

### Packet Flow (Client → Server)

```
WebSocket frame
  → WebSocketConnection.PacketReceived
    → PacketHandlerPlugIn.HandlePacketAsync (deserializes Protobuf)
      → player.InputQueue.Enqueue(IPlayerInput)   ← ConcurrentQueue, max 32
        → GameTickLoop.ProcessInputBuffersAsync()
          → IPlayerInputHandler.HandleAsync(input)
            → PlayerAction (game logic, no network)
              → player.InvokeViewPlugInAsync<IViewPlugIn>(...)
                → ViewPlugIn.XxxAsync (serializes + sends Protobuf)
```

### Packet Flow (Server → Client)

ViewPlugIns in `src/GameServer/RemoteView/Protobuf/` serialize to `ServerEnvelope` Protobuf and write to `IConnection.Output` (PipeWriter). `S2CSnapshot` packets (position deltas) are batched and sent by `BroadcastSnapshotsAsync()` at the end of each tick — never written directly during the physics phase.

### New Protocol (Protobuf + WebSocket)

- `.proto` files live in `src/Network/Protobuf/` (one file per domain: `movement.proto`, `skills.proto`, `items.proto`, `auth.proto`, `world.proto`, `chat.proto`).
- Wire framing: `[4-byte big-endian length][protobuf payload]`. No separate opcode; type is determined by `oneof payload` in `ClientEnvelope` / `ServerEnvelope`.
- All C→S messages prefixed `C2S` (e.g., `C2SMove`); all S→C messages prefixed `S2C`.
- `WebSocketConnection` (`src/Network/WebSocketConnection.cs`) implements `IConnection`. TLS is terminated upstream by nginx — the listener handles plain `ws://`.
- `WebSocketGameServerListener` (`src/GameServer/WebSocketGameServerListener.cs`) uses `HttpListener` on port 5000.

### Coordinate System

Two systems coexist:
- `Vector2F(float X, float Y)` — continuous runtime position sent in the protocol. Scale: 1 tile = 8 units (valid range 0–2040).
- `Point(byte X, byte Y)` — tile position for `WalkMap` collision, bucket indexing, and database storage.

Conversions: `Vector2F.ToTile()` divides by 8 (never cast directly or use `MathF.Floor`). `Vector2F.FromTile(p)` returns the tile center (`p.X * 8f + 4f`).

### World Scale Reference (Godot client)

MU Online is a miniature world — scale everything with this in mind:

| Quantity | Value |
|---|---|
| 1 tile | 0.8 Godot units (`WorldMap.TileSize = 8f * ServerToWorld`) |
| Server coord unit | 0.1 Godot units (`WorldMap.ServerToWorld = 0.1f`) |
| Server unit → tile | server coord / 8 = tile (server sends tile × 8 as float) |
| Full map | 256 tiles × 0.8 = **204.8 × 204.8 Godot units** |
| Character height | ≈ 1.8 Godot units |
| Camera (zoom=1) | BaseDist ≈ 10 units, pitch ≈ 65° — ≈25 tiles visible |

Consequence: `WorldCamera.MaxZoom` must be kept small (≤ 2.2). At BaseDist=10 and zoom=1.7 the camera is ~17 units from the player (~44 tiles visible). The original `BaseDist=31.3` (the MU client's raw camera offset) was 10× too large because `ServerToWorld=0.1` shrinks everything.

### Plugin System

Plugin interfaces require two attributes: `[Guid("...")]` (fixed GUID — required for stable configuration references) and either `[PlugInPoint]`, `[CustomPlugInContainer]`, or `[PlugIn]`. Plugins are discovered at startup and can be activated/deactivated from the admin panel. Custom plugins can be loaded from external assemblies (in a `plugins/` subfolder) or from C# source compiled at runtime by Roslyn.

### Persistence Layer

`IPersistenceContextProvider` is the entry point for all data access. The EF Core implementation (`src/Persistence/EntityFramework/`) does **not** use `DataModel` classes directly — code generation produces derived classes with EF navigation/FK boilerplate. `GameConfiguration` and `Account` are loaded as complete object graphs via PostgreSQL JSON functions (one query each). Each connected player gets its own `IPlayerContext`; saving calls `SaveChanges()` on that context.

### Anti-patterns (auto-rejected in PRs)

- `using MUnique.OpenMU.Network` inside `src/GameLogic/`
- Accepting cooldown/position state from the client as authoritative
- `_context.Remove(character)` — use `character.DeletedAt = DateTime.UtcNow`
- `new Timer(...)` or `Task.Delay` in walker or monster AI code
- Accessing session state (login/logout) outside `ILoginServer`
- `terrain.WalkMap[(byte)pos.X, ...]` — always use `pos.ToTile()`
- Calling a `PlayerAction` directly from a packet handler (must go through `InputQueue`)
- Hardcoded gameplay constants — use `GameConfiguration` from DB
- Branching on legacy protocol version in new code

### Test Accounts (auto-created on DB init)

Accounts `test0`–`test9` (levels 1–90), `test300`, `test400`, `testgm`, `ancient`, `socket`. All passwords equal the username.