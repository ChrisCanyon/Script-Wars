# Turn-Based Resolution + Pokémon-Style Targeting UI — Design

**Date:** 2026-06-21
**Status:** Draft for review
**Scope:** Increment 1 of the "intention-based" turn engine.

## Goal

Convert the arena game from real-time (server ticks every 400ms, arrow keys move
instantly) to a **turn-based, simultaneous-resolution** game with a **Pokémon-style
UI**: the player picks an action from a menu; if the action needs a target, legal
tiles highlight on the board and the player clicks one to commit.

This increment adopts the *shape* of the full "bot turn contract" (observation in →
intention out) so later increments (fog, auras, inventory, real combat) slot in
without reworking the protocol. It implements only a vertical slice of that contract.

## North-Star Contract (reference, not all built now)

The long-term contract is an **observation → intention** exchange:

- The engine sends each actor an **observation** (`tick`, `self`, `visibleActors`,
  `visibleTiles`, `auras`, `availableActions`, `lastTurnResult`, `rules`).
- Each actor replies with an **intention**: `{ actionId, target: { position | actorId
  | self } }`. Missing/invalid/timeout ⇒ `wait`.
- The engine — never the actor — decides outcomes, resolving each tick by
  `phaseOrder` (`movement → attacks → hazards → cleanup`).

Bots submit *intentions, not mutations*. The same contract serves the human client,
server-side bots, external scripts, and replays.

## Increment 1 Scope

### Live now
- **Turn loop — simultaneous resolution.** The 400ms wander loop is removed. The
  server holds one `pending` intention per actor for the current tick. The bot's
  intention is auto-chosen at tick start. The human submits via the UI. When *every*
  actor has a pending intention, the engine resolves the tick, clears pending,
  increments `tick`, and the next tick begins.
- **Phased resolution:** `phaseOrder = ["movement", "attacks", "cleanup"]`
  (`hazards` omitted until auras exist).
- **Action descriptors** carry `{ id, source, targetType, range, tags }`. The client's
  targeting mode is driven entirely by `targetType` and `range`.
  Live actions:
  - `move` — `source: system`, `targetType: tile`, `range: 1`
  - `basic_attack` — `source: ability`, `targetType: actor_or_tile`, `range: 1`
  - `wait` — `source: system`, `targetType: none`, `range: 0`
  - `healing_potion` — `source: inventory`, `targetType: self` — **defined but not
    offered** this increment (proves menu is data-driven; enabling it later is data-only).
- **Intention reply shape:** `{ actionId, target: { position {x,y} | actorId | self } }`;
  bare `{ actionId: "wait" }` for no-target. Bot and human submit the same shape.
- **Shared `Core` library** owns the contract types, action definitions, and the pure
  rules. Server validates submissions with it; client computes tile highlights with it.
- **Client UI:** action menu → select → if `targetType != none`, highlight legal tiles
  (via `Core.LegalTargets`) → click to commit. Cancel via **Esc**, **right-click**, or
  **re-clicking the selected/another menu item**.

### Combat this increment: wired but inert
`basic_attack` resolves end-to-end and emits a `damage` event in `lastTurnResult`, but
**hp does not change and nobody dies**. `hp`/`maxHp` are carried in state for the UI to
read later. Animations, health bars, and real damage math are a later increment.

### Movement conflicts this increment
- `movementConflictRule: contested_tile_blocks_all` — if two+ actors target the same
  destination tile, **all of them stay on their starting tile**.
- `swapRule: direct_swaps_blocked` — if two actors try to trade places, **both stay put**.
- On either kind of bump, each involved actor emits a **melee `damage` event** against
  the other (inert, per "wired but inert" above). Every actor implicitly has the
  range-1 `basic_attack`, so a future flip to real damage makes bumping trade hits.

### Stubbed (field present, mechanic later)
`teamId` (player vs bot), `hp`/`maxHp`, `statusEffects: []`, `tick`, `lastTurnResult`,
`events`.

### Deferred (later increments)
Fog of view (`visibleTiles`/`visibleActors` send the **whole board** for now),
auras/`hazards` phase, inventory items, `maxThinkMs` enforcement, status-effect logic,
multiple bots/teams beyond the current player + one bot.

## Architecture

```
arena-game-csharp/
  Core/                     <- NEW shared class library (ArenaGame.Core)
    Core.csproj
    Contract/               <- observation + intention DTOs (the wire shape)
      Observation.cs        <- ObservationDto, SelfDto, ActorDto, TileDto, ActionDto, LastTurnResultDto, EventDto, RulesDto
      Intention.cs          <- IntentionDto, TargetDto (position | actorId | self)
    Actions.cs              <- ActionDef registry { Id, Source, TargetType, Range, Tags }; TargetType enum
    Rules.cs                <- pure functions over a state snapshot:
                               LegalTargets(state, actorId, actionId) -> IEnumerable<(int x,int y)>
                               IsLegal(state, actorId, intention) -> bool
                               Resolve(state, intentions) -> (newState, events)  // phased
    StateModel.cs           <- the snapshot both sides reason about (grid + actors)
  Server/                   <- references Core
    Program.cs              <- endpoints + tick orchestration
    Game/World.cs           <- authoritative state; holds pending intentions; uses Core.Rules
  Client/                   <- references Core
    Program.cs              <- Raylib UI; menu + targeting; uses Core.LegalTargets to highlight
```

`Entity.cs`/`World.cs` move their rule logic into `Core`; `World` keeps only the
authoritative mutable state + pending-intention bookkeeping and delegates to `Core.Rules`.

### Why a shared library
One source of truth for rules. The **client computes** legal target tiles locally (no
round-trip, instant highlight) by calling the same `Core.LegalTargets` the **server**
uses in `IsLegal` to validate submissions. Adding an action later is a single
`ActionDef` + rule branch, picked up by both sides.

## Protocol

### `GET /state` (observation)
Returns the full board for the player's perspective. Increment-1 payload (whole board,
no fog):

```json
{
  "tick": 42,
  "width": 12,
  "height": 12,
  "rules": {
    "phaseOrder": ["movement", "attacks", "cleanup"],
    "movementConflictRule": "contested_tile_blocks_all",
    "swapRule": "direct_swaps_blocked",
    "missingAction": "wait"
  },
  "self": { "id": "player", "type": "player", "teamId": "players", "hp": 100, "maxHp": 100,
            "position": { "x": 6, "y": 6 }, "statusEffects": [] },
  "visibleActors": [
    { "id": "bot", "type": "bot", "teamId": "bots", "hp": 30, "maxHp": 30,
      "position": { "x": 1, "y": 1 }, "statusEffects": [] }
  ],
  "visibleTiles": [ { "x": 0, "y": 0, "terrain": "stone", "blocked": false }, "... whole board ..." ],
  "auras": [],
  "availableActions": [
    { "id": "move",         "source": "system",  "targetType": "tile",          "range": 1, "tags": ["movement"] },
    { "id": "basic_attack", "source": "ability", "targetType": "actor_or_tile", "range": 1, "tags": ["melee","damage"] },
    { "id": "wait",         "source": "system",  "targetType": "none",          "range": 0, "tags": [] }
  ],
  "lastTurnResult": {
    "tick": 41,
    "submittedAction": { "actionId": "basic_attack", "target": { "actorId": "bot" } },
    "resolved": true,
    "events": [ { "type": "damage", "sourceId": "player", "targetId": "bot", "amount": 1 } ]
  },
  "playerSubmitted": false
}
```

`playerSubmitted` lets the UI show a "waiting…" state and is the signal the client uses,
along with a change in `tick`, to clear its local selection when a new tick begins.

### `POST /action` (intention)
```json
{ "actorId": "player", "actionId": "move", "target": { "position": { "x": 6, "y": 5 } } }
```
- `target` is omitted for `targetType: none` (`wait`), `{ "self": true }` for self,
  `{ "actorId": "..." }` or `{ "position": {x,y} }` for board targets.
- Server validates with `Core.Rules.IsLegal`. Invalid ⇒ rejected (the human re-picks;
  a bot's invalid/missing intention ⇒ `wait`).
- On accept, the actor's `pending` intention is set. When all actors are pending, the
  tick resolves.

## Resolution Algorithm (`Core.Rules.Resolve`)
Per tick, given all actors' intentions:
1. Default any missing/invalid intention to `wait`.
2. **Movement phase:** gather move targets. Apply `contested_tile_blocks_all` (drop all
   moves into a contested tile) and `direct_swaps_blocked` (drop both sides of a swap).
   Surviving moves apply; blocked actors stay and each emits a melee `damage` event vs
   the actor they collided with.
3. **Attacks phase:** resolve `basic_attack` against **post-movement** positions. Emit a
   `damage` event (inert — no hp change this increment).
4. **Cleanup phase:** no-op this increment (no deaths/status yet); reserved hook.
5. Record `events` and `submittedAction` into `lastTurnResult`; increment `tick`.

## Client UI Flow
1. Render board + actors (existing sprite rendering retained; rectangle fallbacks ok).
2. Draw the action menu from `availableActions` (Pokémon-style list at bottom).
3. Player clicks an action:
   - `targetType: none` ⇒ submit immediately (`wait`).
   - `targetType: self` ⇒ submit immediately with `{ self: true }`.
   - `tile` / `actor_or_tile` ⇒ enter **targeting mode**: call `Core.LegalTargets`,
     highlight those tiles; click a highlighted tile/actor to submit.
4. **Cancel:** Esc, right-click, or re-clicking the selected menu item (or another item)
   returns to the menu without spending the tick.
5. After submit, show "waiting…" until `tick` advances, then clear selection and re-enable
   the menu for the new tick.

## Testing
- `Core.Rules` is pure ⇒ unit-tested directly (no HTTP, no Raylib):
  - `LegalTargets` for `move`/`basic_attack` at range 1, board edges, occupied tiles.
  - `IsLegal` accepts valid, rejects out-of-range / wrong-target-type / off-board.
  - `Resolve`: clean moves; `contested_tile_blocks_all`; `direct_swaps_blocked`; bump
    emits melee events; attack emits inert damage event; missing intention ⇒ wait.
- Server: `/state` returns the contract shape; `/action` accepts valid, rejects invalid,
  resolves a tick when all actors are pending.
- Client UI is verified manually on Windows (Raylib needs a real display): menu renders,
  selecting Move highlights adjacent tiles, click moves, Esc/right-click/re-click cancels,
  Attack highlights the adjacent bot, Wait passes the tick.

## Out of Scope (explicit)
Real damage/death, health bars, attack animations, fog of view, auras/hazards, inventory
items, status effects, think-time limits, more than one bot. All are designed to layer
onto this contract without protocol rework.
