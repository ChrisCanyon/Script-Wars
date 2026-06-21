# Arena Game — Session Handoff / Context Dump

**Last updated:** 2026-06-20
**Repo:** `C:\TylerDev\arena-game-csharp` (WSL: `/mnt/c/TylerDev/arena-game-csharp`)
**Status:** MVP COMPLETE. Server cleaned + Raylib-cs sprite client built. Solution builds
clean (0 warnings/0 errors). Server protocol verified end-to-end. Real sprite art dropped in.
Remaining gap: the Raylib window has only been built, not visually run/verified (must run on
Windows — see note below).

---

## What this project is

A **new, novel coding-interview game** ("Gold Rush Arena") to test a friend —
deliberately NOT the official Tyler Technologies "prize game" (that one lives in the
separate repo `C:\TylerDev\prize-game-csharp`). The full design brainstorm is in the
*old* repo at `prize-game-csharp/docs/brainstorm/2026-06-19-arena-game-ideas.md`.

**Core architecture principle:** a deterministic C# game core behind a clean
`state -> action` JSON boundary. The server is the source of truth and NEVER runs
participant code — it only exchanges JSON. The client renders and sends back a chosen
action picked from a list the **server** provides.

### Immediate goal (this session's scope)
A simple **C# server + C# client**:
- Server: holds the world, exposes state + the list of legal actions, accepts actions.
- Client: renders a 2D grid, reads input, sends one of the server-provided actions.
- First actions: `MoveUp / MoveDown / MoveLeft / MoveRight`.

### Decisions locked this session
- **Client rendering:** dependency-free **C# console client** first (character grid +
  arrow keys). Chosen for simplicity + to defer the graphics-engine choice. The
  server↔client protocol is identical to a future graphics client, so swapping in
  Raylib-cs / MonoGame sprites later is a drop-in change.
- **Transport:** HTTP + JSON (server is an ASP.NET minimal-API web app). Client uses
  `HttpClient` to GET `/state` and POST `/action`.
- **Action list comes FROM the server** (in the `/state` payload), not hardcoded in the
  client. This is a real design requirement — server is the authority on legality.
- **.NET version:** targeting **net8.0**. The machine ONLY has the **.NET 8 SDK
  (8.0.400)** installed — NO .NET 10. User asked about net10; to use it they'd need to
  install the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`) and bump both
  csproj `<TargetFramework>` to `net10.0`.

---

## Toolchain notes (IMPORTANT)

- `dotnet` is NOT on the Linux PATH. It only exists on the Windows side:
  **`/mnt/c/Program Files/dotnet/dotnet.exe`** (quote the space).
- Example build from WSL:
  `"/mnt/c/Program Files/dotnet/dotnet.exe" build`
- The prior (dead) session left a server running on `localhost:5000` that locked
  `bin/`. It was killed via
  `/mnt/c/Windows/System32/taskkill.exe /F /IM arena-game-csharp.exe` and
  `taskkill.exe /F /IM dotnet.exe`. If binaries are locked again, kill the process.

---

## Current file layout

```
arena-game-csharp/
  HANDOFF.md              <- this file
  arena-game-csharp.sln   <- references Server + Client  [DONE]
  Server/
    Server.csproj         <- net8.0 web app, AssemblyName "arena-server"  [DONE]
    Program.cs            <- minimal-API server  [CLEANED]
    Game/
      Entity.cs           <- Id, Kind ("player"|"bot"), X, Y  [unchanged]
      World.cs            <- game core; entities, Apply/Step/Snapshot, AvailableActions  [DONE]
  Client/
    Client.csproj         <- net8.0 Exe, AssemblyName "arena-client", Raylib-cs 6.1.1  [DONE]
    Program.cs            <- Raylib sprite client (poll /state, draw, send actions)  [DONE]
    assets/               <- player.png, bot.png, floor.png, wall.png  [DROPPED IN]
```
The old browser client (`wwwroot/index.html`) and root `arena-game-csharp.csproj` were
DELETED on purpose in the prior session.

---

## What's DONE

1. **Restructured** root web project into `Server/` and created empty `Client/`.
   Removed browser client (`wwwroot/`) and old root csproj + `bin/`/`obj/`.
2. **`Server/Server.csproj`** written (net8.0, Sdk.Web, AssemblyName `arena-server`).
3. **`Server/Game/World.cs`** updated:
   - Added `public static readonly string[] AvailableActions = { "MoveUp","MoveDown","MoveLeft","MoveRight" };`
   - `Snapshot()` now includes `actions = AvailableActions` in the JSON payload.

### The `/state` JSON shape the client will consume
```json
{
  "width": 12,
  "height": 12,
  "actions": ["MoveUp", "MoveDown", "MoveLeft", "MoveRight"],
  "entities": [
    { "id": "player", "kind": "player", "x": 6, "y": 6 },
    { "id": "bot",    "kind": "bot",    "x": 1, "y": 1 }
  ]
}
```
### Server endpoints (in `Server/Program.cs`)
- `GET  /state`  -> `world.Snapshot()`
- `POST /action` body `{ "entityId": "player", "type": "MoveUp" }` -> `world.Apply(...)`
- Background `Task` ticks `world.Step()` every 400ms (bot wanders randomly).
- Runs on `http://localhost:5000`.

---

## What was done 2026-06-20 (MVP)

Decision change: skipped the throwaway console client and went straight to a **Raylib-cs
sprite client** (user wanted real sprites; AI mocks them quickly). Two parallel agents
built the two halves; coordinator did the .sln + build + verify.

1. **`Server/Program.cs` cleaned** — removed `app.UseDefaultFiles()` / `app.UseStaticFiles()`.
   Everything else (tick loop, `/state`, `/action`, `ActionRequest`) untouched.
2. **`Client/Client.csproj`** — `Microsoft.NET.Sdk`, net8.0, Exe, AssemblyName `arena-client`,
   RootNamespace `ArenaGame.Client`, `PackageReference Raylib-cs 6.1.1`, and an
   `assets/**/*` → `CopyToOutputDirectory=PreserveNewest` item.
3. **`Client/Program.cs`** — background task GETs `/state` every 100ms into a lock-guarded
   field; Raylib render loop draws floor (floor.png tiled, else flat color + grid lines) +
   entities (player.png/bot.png scaled to 48px; **colored-rectangle fallback** if a texture
   is missing) + a bottom legend of the server's `actions`. Arrow keys map to action
   strings and are **only sent if present in the server's `actions` list**. Window sized
   from first `/state` (12×12 → 576×576). Tile coords map directly to pixels (y-down matches
   Raylib, no flip).
4. **`arena-game-csharp.sln`** at repo root references both projects. `dotnet build` of the
   sln is clean (0/0).
5. **Real assets dropped in** `Client/assets/`: `player.png` (knight), `bot.png` (robot),
   `floor.png` (cobblestone), `wall.png` (unused so far). All 256×256; player/bot RGBA
   (transparent), floor/wall RGB. They copy into the client's output folder on build.

### Server verified (from Windows PowerShell — see toolchain note)
GET `/state` returns the 12×12 world + actions `[MoveUp,MoveDown,MoveLeft,MoveRight]`.
POST `MoveUp` then `MoveLeft` moved the player (6,6)→(5,5). Protocol works.

## What's LEFT

1. **Visually run the client** (must be done on Windows — Raylib needs a real display;
   WSL can't reach the Windows-bound server anyway). Start server then client on Windows;
   confirm the knight renders, arrow keys move it, the robot wanders. This is the one
   unverified piece.
2. Optional polish: use `wall.png` for a border, smooth movement/animation, window title
   tweaks — none required for MVP.
3. Then resume the real game design (gold/combat/fog) behind the same `state -> action`
   boundary.

---

## Design context for judgment calls (from the brainstorm)

Full game (future, not now): grid arena, 4 players, 10 HP, attack=1. Per-turn menu is
Move U/D/L/R or Attack (attack hits all adjacent). Pickups: gold (carried, at-risk),
shield (one-shot block), health. Bank carried gold at home base -> permanent score; most
banked wins. Death drops unbanked gold; respawn. Fog of war (~5 tiles). The candidate's
gradeable hook is a single `Decide(state)` method returning one action from the menu,
given a fog-filtered read-only view. We are building the rendering/protocol skeleton
first; the gold/combat/fog mechanics come later behind the same `state -> action`
boundary.

---

## Resume checklist (read these first next session)
1. This file.
2. `Server/Game/World.cs` and `Server/Program.cs` (current server truth).
3. The brainstorm doc in the OLD repo:
   `C:\TylerDev\prize-game-csharp\docs\brainstorm\2026-06-19-arena-game-ideas.md`.
4. Then resume at "What's LEFT" step 1.
