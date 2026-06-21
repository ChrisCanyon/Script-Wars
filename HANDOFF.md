# Arena Game — Session Handoff / Context Dump

**Last updated:** 2026-06-21
**Repo:** `C:\TylerDev\arena-game-csharp` (WSL: `/mnt/c/TylerDev/arena-game-csharp`)
**Git:** now a git repo (branch `main`), pushed to `origin` =
https://github.com/ChrisCanyon/Script-Wars (remote's original LICENSE preserved via an
unrelated-histories merge). Identity set repo-locally (Chris Nagy / christopher.nagy@tylertech.com).
**Status:** TURN-BASED + Pokémon-style targeting UI COMPLETE (13 commits, 10 TDD tasks).
Solution builds clean (0/0). Core rules engine has 19/19 passing unit tests. Two gaps below:
(1) the Raylib client UI has NOT been visually run (Raylib needs a Windows display; WSL can't
reach the Windows-bound server either); (2) a sprite-rendering conflict with in-flight WIP —
see "What's LEFT".

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
  arena-game-csharp.sln   <- references Core + Core.Tests + Server + Client
  docs/superpowers/        <- spec + plan for the turn-based work (2026-06-21-turn-based-targeting*)
  Core/                    <- NEW shared lib (ArenaGame.Core); single source of truth for rules
    Contract.cs           <- ObservationDto/SelfDto/ActorDto/TileDto/RulesDto/LastTurnResultDto + Intention/Target
    Actions.cs            <- ActionDef registry; OfferedIds = move/basic_attack/wait (healing_potion defined, not offered)
    StateModel.cs         <- StateModel + ActorState; FromObservation()
    Position.cs           <- Position, GameEvent
    Rules.cs              <- LegalTargets / IsLegal / Resolve (pure; used by BOTH server + client)
  Core.Tests/             <- xUnit; 19 tests over Rules (LegalTargets/IsLegal/Resolve)
  Server/
    Server.csproj         <- net8.0 web app, refs Core, AssemblyName "arena-server"
    Program.cs            <- minimal API: GET /state (observation), POST /action (intention) — NO tick loop
    Game/World.cs         <- authoritative StateModel + _pending intentions + tick; delegates to Core.Rules
    (Entity.cs DELETED — replaced by ArenaGame.Core.ActorState)
  Client/
    Client.csproj         <- net8.0 Exe, refs Core, Raylib-cs 6.1.1
    Program.cs            <- Raylib client: poll observation, menu, targeting highlights, submit intentions
    PlayerPalette.cs      <- recolors a sentinel-marked sprite into per-team variants (Pick by id)
    assets/               <- bot.png, floor.png, wall.png, player.png, player/{up,down,left,right}/ (WIP frames)
```
The old browser client and root csproj were deleted in an earlier session.

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
### Server endpoints (in `Server/Program.cs`) — CURRENT (turn-based)
- `GET  /state`  -> `world.Snapshot()` returns an **ObservationDto** (tick, self, visibleActors,
  whole-board visibleTiles, availableActions [move/basic_attack/wait], rules, lastTurnResult, playerSubmitted).
- `POST /action` body is an **Intention** `{ "actorId":"player", "actionId":"move", "target":{"position":{"x":6,"y":5}} }`
  -> `world.Submit(...)`; validated by `Rules.IsLegal` (200 ok / 400 illegal).
- **No background tick loop.** The bot auto-readies each tick; the world resolves when all actors are pending.
- Runs on `http://localhost:5000`.
- NOTE: the old `{ "entityId", "type":"MoveUp" }` / `Apply` / `Step` API and the MoveUp/Down/Left/Right
  action set are GONE. The `/state` JSON example shown earlier in this doc is the OLD shape (superseded).

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

## What was done 2026-06-21 (turn-based + targeting UI)

Converted real-time → **turn-based, simultaneous resolution** with a **Pokémon-style
action→target UI**. Spec + plan in `docs/superpowers/`. Built via subagent-driven TDD
(10 tasks, per-task reviews + a final whole-branch review). Key decisions:

1. **Shared `Core` library** holds the wire contract and the pure rules. The **client computes
   tile highlights** with `Core.Rules.LegalTargets` and the **server validates** submissions with
   `Core.Rules.IsLegal` — same code path, so a client-offered tile can't be server-rejected.
2. **Intention-based contract** (observation in → intention out), shaped to match the longer-term
   bot contract (teams, hp, statusEffects, lastTurnResult/events are present as fields). Fog,
   auras, inventory, real damage/death, >1 bot are deliberately DEFERRED.
3. **Resolution** by phaseOrder `[movement, attacks, cleanup]`. Movement conflicts:
   `contested_tile_blocks_all` + `direct_swaps_blocked`; a blocked actor stays put and trades a
   **mutual melee "damage" event** with whoever it bumped (incl. walking into a stationary actor).
   **Combat is wired-but-INERT**: events are emitted, hp never changes, nobody dies.
4. **Client UI**: data-driven menu from `availableActions`; pick an action → if it needs a target,
   legal tiles highlight → click to commit; cancel via Esc / right-click / re-clicking the action.
   Waiting state clears on tick advance and recovers from a rejected/failed POST.

## What's LEFT

1. **SPRITE CONFLICT (do this first).** There is in-flight WIP (UNCOMMITTED in the working tree):
   `player-palette.png` was deleted, `PlayerPalette.cs` has a param rename, `README.txt` edited,
   and `assets/player/{up,down,left,right}/` directional frames were added. But the committed
   client code loads `player-palette.png` (which no longer exists) → `PlayerPalette.Pick` returns
   default → **the player renders as a fallback blue rectangle**. Decide: wire the new directional
   frames into `Client/Program.cs` `DrawActor`, OR restore `player-palette.png`. Commit the WIP
   once reconciled.
2. **Visually run the client** (Windows only — Raylib needs a display; WSL can't reach the
   Windows-bound server). Confirm: menu renders; Move highlights up-to-4 adjacent tiles and click
   moves the player + advances the tick; Attack highlights the adjacent bot; Wait passes; Esc/
   right-click/re-click cancel. This is unverified — the build + 19 Core tests are green, but the
   UI itself has never been run.
3. **Next increment (designed to slot in without protocol rework):** fog of view (filter
   `visibleActors`/`visibleTiles` in `Snapshot`), real combat (apply `events` to hp in the reserved
   cleanup hook + health bars/animations), inventory/`healing_potion`, more bots/teams
   (`AutoPickBot` → loop; `TryResolve` already supports N actors). Also flagged for multi-actor:
   extract a shared move-shape helper for `LegalTargets`+`Resolve`; handle 3+ actor chained-vacate
   and the O(n²) swap-loop double-emit (fine for player+1-bot today).

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
2. `Core/Rules.cs` + `Core/Contract.cs` + `Core/Actions.cs` (the rules + wire contract — the heart).
3. `Server/Game/World.cs` and `Server/Program.cs` (turn loop + endpoints).
4. `Client/Program.cs` (menu/targeting/submit) and `Client/PlayerPalette.cs` (for the sprite conflict).
5. The spec + plan: `docs/superpowers/specs/2026-06-21-turn-based-targeting-design.md` and
   `docs/superpowers/plans/2026-06-21-turn-based-targeting.md`.
6. The brainstorm doc in the OLD repo:
   `C:\TylerDev\prize-game-csharp\docs\brainstorm\2026-06-19-arena-game-ideas.md`.
7. Then resume at "What's LEFT" step 1 (sprite conflict), then step 2 (run the client on Windows).
