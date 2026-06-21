# Turn-Based Resolution + Pokémon-Style Targeting UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the arena game from real-time to a turn-based, simultaneous-resolution game with a Pokémon-style action→target UI, with all rules in a shared `Core` library used by both server and client.

**Architecture:** A new `Core` class library holds the wire contract (observation/intention DTOs), the action registry, and pure rules functions (`LegalTargets`, `IsLegal`, `Resolve`). The server owns authoritative state, validates submissions and resolves ticks via `Core`; the client renders the menu and computes tile highlights via the same `Core` functions. Actors submit *intentions*; the engine decides outcomes.

**Tech Stack:** C# / .NET 8, ASP.NET minimal API (server), Raylib-cs 6.1.1 (client), xUnit (Core tests), System.Text.Json.

## Global Constraints

- **Target framework:** `net8.0` for every project (only the .NET 8 SDK is installed).
- **dotnet is Windows-only:** invoke as `"/mnt/c/Program Files/dotnet/dotnet.exe"` (quote the space). Set `DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"` in your shell for the commands below.
- **Git:** this directory is NOT a git repo. If you want the commit steps to work, run `git init` once before Task 1; otherwise skip every "Commit" step. Commits are optional and never block a task.
- **JSON shape:** the wire format is camelCase (ASP.NET's default `Results.Json` and `System.Text.Json` defaults). Client reads with `PropertyNameCaseInsensitive = true`. Do not hand-write `[JsonPropertyName]` attributes — rely on camelCase defaults.
- **Server is the authority on legality.** The client computing highlights is a UX convenience; every submitted intention is re-validated server-side with `Core.Rules.IsLegal`.
- **Combat is wired but inert this increment:** `Resolve` emits `damage` events but never changes `hp` and never removes actors.
- **Namespaces:** all `Core` types live in namespace `ArenaGame.Core`.
- If a Raylib window cannot be opened in your environment (WSL has no display), client UI steps are verified by building successfully and by a Windows run noted in the task; do not claim visual success without a real run.

---

## File Structure

```
Core/                       <- NEW class library (ArenaGame.Core)
  Core.csproj
  Position.cs               <- Position, GameEvent records
  Actions.cs                <- ActionDef record + Actions registry
  StateModel.cs             <- StateModel, ActorState; FromObservation()
  Contract.cs               <- ObservationDto, SelfDto, ActorDto, TileDto, RulesDto, LastTurnResultDto + Intention, Target
  Rules.cs                  <- LegalTargets, IsLegal, Resolve
Core.Tests/                 <- NEW xUnit test project
  Core.Tests.csproj
  RulesTests.cs
Server/
  Game/World.cs             <- MODIFY: state + pending intentions + tick; delegates to Core.Rules
  Program.cs                <- MODIFY: remove wander loop; /state observation, /action intention
Client/
  Program.cs                <- MODIFY: reference Core; menu + targeting UI; submit intentions
arena-game-csharp.sln       <- MODIFY: add Core and Core.Tests
```

---

### Task 1: Create the Core library, contract types, and action registry

**Files:**
- Create: `Core/Core.csproj`
- Create: `Core/Position.cs`
- Create: `Core/Actions.cs`
- Create: `Core/StateModel.cs`
- Create: `Core/Contract.cs`
- Modify: `arena-game-csharp.sln`

**Interfaces:**
- Produces:
  - `record Position(int X, int Y)`
  - `record GameEvent(string Type, string SourceId, string? TargetId, int Amount)`
  - `record ActionDef(string Id, string Source, string TargetType, int Range, string[] Tags)`
  - `static class Actions` with `IReadOnlyDictionary<string, ActionDef> All` and `string[] OfferedIds` (the ids offered this increment) and `ActionDef[] OfferedFor(StateModel state, string actorId)` returning the descriptor list for the observation.
  - `class ActorState { string Id; string Type; string TeamId; int Hp; int MaxHp; int X; int Y; string[] StatusEffects; }`
  - `class StateModel { int Width; int Height; List<ActorState> Actors; ActorState? ActorAt(int x,int y); ActorState? ById(string id); static StateModel FromObservation(ObservationDto obs); }`
  - Contract records: `ObservationDto`, `SelfDto`, `ActorDto`, `TileDto`, `RulesDto`, `LastTurnResultDto`, `Intention`, `Target` (exact shapes below).

- [ ] **Step 1: Create the Core project file**

`Core/Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>ArenaGame.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Add value types**

`Core/Position.cs`:
```csharp
namespace ArenaGame.Core;

public record Position(int X, int Y);

// Emitted by Resolve. Inert this increment (Amount is informational; hp is unchanged).
public record GameEvent(string Type, string SourceId, string? TargetId, int Amount);
```

- [ ] **Step 3: Add the contract DTOs (the wire shape)**

`Core/Contract.cs`:
```csharp
namespace ArenaGame.Core;

// ---- Observation (server -> actor), camelCase on the wire ----
public record ObservationDto(
    int Tick,
    int Width,
    int Height,
    RulesDto Rules,
    SelfDto Self,
    ActorDto[] VisibleActors,
    TileDto[] VisibleTiles,
    object[] Auras,                 // always [] this increment
    ActionDef[] AvailableActions,
    LastTurnResultDto? LastTurnResult,
    bool PlayerSubmitted);

public record RulesDto(
    string[] PhaseOrder,
    string MovementConflictRule,
    string SwapRule,
    string MissingAction);

public record SelfDto(
    string Id, string Type, string TeamId,
    int Hp, int MaxHp, Position Position, string[] StatusEffects);

public record ActorDto(
    string Id, string Type, string TeamId,
    int Hp, int MaxHp, Position Position, string[] StatusEffects);

public record TileDto(int X, int Y, string Terrain, bool Blocked);

public record LastTurnResultDto(
    int Tick, Intention? SubmittedAction, bool Resolved, GameEvent[] Events);

// ---- Intention (actor -> server) ----
public record Intention(string ActorId, string ActionId, Target? Target = null);

// Exactly one of Position / ActorId / Self is meaningful per the action's TargetType.
public record Target(Position? Position = null, string? ActorId = null, bool Self = false);
```

- [ ] **Step 4: Add the action registry**

`Core/Actions.cs`:
```csharp
namespace ArenaGame.Core;

public record ActionDef(string Id, string Source, string TargetType, int Range, string[] Tags);

public static class Actions
{
    // TargetType values match the contract verbatim: "tile" | "actor_or_tile" | "self" | "none".
    public static readonly IReadOnlyDictionary<string, ActionDef> All = new Dictionary<string, ActionDef>
    {
        ["move"]           = new("move",           "system",    "tile",          1, new[] { "movement" }),
        ["basic_attack"]   = new("basic_attack",   "ability",   "actor_or_tile", 1, new[] { "melee", "damage" }),
        ["wait"]           = new("wait",           "system",    "none",          0, Array.Empty<string>()),
        // Defined but NOT offered this increment (proves the menu is data-driven).
        ["healing_potion"] = new("healing_potion", "inventory", "self",          0, new[] { "heal" }),
    };

    // The ids offered to actors this increment, in menu order.
    public static readonly string[] OfferedIds = { "move", "basic_attack", "wait" };

    public static ActionDef[] OfferedFor(StateModel state, string actorId)
        => OfferedIds.Select(id => All[id]).ToArray();
}
```

- [ ] **Step 5: Add the state model**

`Core/StateModel.cs`:
```csharp
namespace ArenaGame.Core;

public class ActorState
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string TeamId { get; init; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string[] StatusEffects { get; set; } = Array.Empty<string>();

    public ActorState Clone() => new()
    {
        Id = Id, Type = Type, TeamId = TeamId, Hp = Hp, MaxHp = MaxHp,
        X = X, Y = Y, StatusEffects = (string[])StatusEffects.Clone()
    };
}

public class StateModel
{
    public int Width { get; init; }
    public int Height { get; init; }
    public List<ActorState> Actors { get; init; } = new();

    public ActorState? ById(string id) => Actors.FirstOrDefault(a => a.Id == id);
    public ActorState? ActorAt(int x, int y) => Actors.FirstOrDefault(a => a.X == x && a.Y == y);
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public StateModel Clone() => new()
    {
        Width = Width, Height = Height,
        Actors = Actors.Select(a => a.Clone()).ToList()
    };

    // Build a rules-ready snapshot from a received observation (used by the client).
    public static StateModel FromObservation(ObservationDto obs)
    {
        var actors = new List<ActorState>
        {
            new() { Id = obs.Self.Id, Type = obs.Self.Type, TeamId = obs.Self.TeamId,
                    Hp = obs.Self.Hp, MaxHp = obs.Self.MaxHp,
                    X = obs.Self.Position.X, Y = obs.Self.Position.Y,
                    StatusEffects = obs.Self.StatusEffects }
        };
        actors.AddRange(obs.VisibleActors.Select(a => new ActorState
        {
            Id = a.Id, Type = a.Type, TeamId = a.TeamId, Hp = a.Hp, MaxHp = a.MaxHp,
            X = a.Position.X, Y = a.Position.Y, StatusEffects = a.StatusEffects
        }));
        return new StateModel { Width = obs.Width, Height = obs.Height, Actors = actors };
    }
}
```

- [ ] **Step 6: Add Core and a placeholder Core.Tests to the solution; build**

The test project is created in Task 2; add Core now and build.
```bash
$DOTNET sln arena-game-csharp.sln add Core/Core.csproj
$DOTNET build Core/Core.csproj
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
git add Core/ arena-game-csharp.sln
git commit -m "feat(core): add shared contract, action registry, and state model"
```

---

### Task 2: Core.Rules.LegalTargets

**Files:**
- Create: `Core.Tests/Core.Tests.csproj`
- Create: `Core.Tests/RulesTests.cs`
- Create: `Core/Rules.cs`
- Modify: `arena-game-csharp.sln`

**Interfaces:**
- Consumes: `StateModel`, `ActorState`, `Position`, `Actions` (Task 1).
- Produces: `static class Rules` with `static List<Position> LegalTargets(StateModel state, string actorId, string actionId)`.

- [ ] **Step 1: Create the test project and wire references**

`Core.Tests/Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Core/Core.csproj" />
  </ItemGroup>
</Project>
```
```bash
$DOTNET sln arena-game-csharp.sln add Core.Tests/Core.Tests.csproj
```

- [ ] **Step 2: Write the failing tests**

`Core.Tests/RulesTests.cs`:
```csharp
using ArenaGame.Core;
using Xunit;

public class RulesTests
{
    static StateModel TwoActors(int px, int py, int bx, int by, int w = 12, int h = 12) => new()
    {
        Width = w, Height = h,
        Actors =
        {
            new ActorState { Id = "player", Type = "player", TeamId = "players", Hp = 100, MaxHp = 100, X = px, Y = py },
            new ActorState { Id = "bot",    Type = "bot",    TeamId = "bots",    Hp = 30,  MaxHp = 30,  X = bx, Y = by },
        }
    };

    [Fact]
    public void Move_offers_four_adjacent_in_bounds_unoccupied_tiles()
    {
        var s = TwoActors(5, 5, 1, 1);
        var t = Rules.LegalTargets(s, "player", "move");
        Assert.Equal(4, t.Count);
        Assert.Contains(new Position(5, 4), t);
        Assert.Contains(new Position(5, 6), t);
        Assert.Contains(new Position(4, 5), t);
        Assert.Contains(new Position(6, 5), t);
    }

    [Fact]
    public void Move_excludes_off_board_tiles_at_corner()
    {
        var s = TwoActors(0, 0, 5, 5);
        var t = Rules.LegalTargets(s, "player", "move");
        Assert.Equal(2, t.Count); // only (1,0) and (0,1)
    }

    [Fact]
    public void Move_excludes_tile_occupied_by_another_actor()
    {
        var s = TwoActors(5, 5, 5, 4); // bot directly above player
        var t = Rules.LegalTargets(s, "player", "move");
        Assert.DoesNotContain(new Position(5, 4), t);
        Assert.Equal(3, t.Count);
    }

    [Fact]
    public void Attack_offers_adjacent_tiles_including_occupied()
    {
        var s = TwoActors(5, 5, 5, 4); // bot adjacent above
        var t = Rules.LegalTargets(s, "player", "basic_attack");
        Assert.Contains(new Position(5, 4), t); // can target the bot's tile
        Assert.Equal(4, t.Count);
    }

    [Fact]
    public void Wait_has_no_targets()
    {
        var s = TwoActors(5, 5, 1, 1);
        Assert.Empty(Rules.LegalTargets(s, "player", "wait"));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: FAIL — `Rules` does not exist / does not compile.

- [ ] **Step 4: Implement LegalTargets**

`Core/Rules.cs`:
```csharp
namespace ArenaGame.Core;

public static class Rules
{
    static readonly (int dx, int dy)[] Adjacent = { (0, -1), (0, 1), (-1, 0), (1, 0) };

    public static List<Position> LegalTargets(StateModel state, string actorId, string actionId)
    {
        var result = new List<Position>();
        var actor = state.ById(actorId);
        if (actor is null || !Actions.All.TryGetValue(actionId, out var def)) return result;

        switch (def.TargetType)
        {
            case "tile": // movement: in-bounds, unoccupied, within range (range 1 = adjacent)
                foreach (var (dx, dy) in Adjacent)
                {
                    int nx = actor.X + dx, ny = actor.Y + dy;
                    if (state.InBounds(nx, ny) && state.ActorAt(nx, ny) is null)
                        result.Add(new Position(nx, ny));
                }
                break;

            case "actor_or_tile": // attack: any in-bounds adjacent tile (occupied or not)
                foreach (var (dx, dy) in Adjacent)
                {
                    int nx = actor.X + dx, ny = actor.Y + dy;
                    if (state.InBounds(nx, ny))
                        result.Add(new Position(nx, ny));
                }
                break;

            // "self" and "none" have no board targets.
        }
        return result;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: PASS (5 passed).

- [ ] **Step 6: Commit**

```bash
git add Core/Rules.cs Core.Tests/ arena-game-csharp.sln
git commit -m "feat(core): legal-target computation for move and attack"
```

---

### Task 3: Core.Rules.IsLegal

**Files:**
- Modify: `Core/Rules.cs`
- Modify: `Core.Tests/RulesTests.cs`

**Interfaces:**
- Consumes: `LegalTargets`, `Intention`, `Target`, `Actions`.
- Produces: `static bool IsLegal(StateModel state, Intention intention)`.

- [ ] **Step 1: Write the failing tests**

Append to `Core.Tests/RulesTests.cs` (inside the class):
```csharp
    [Fact]
    public void IsLegal_accepts_move_to_adjacent_tile()
    {
        var s = TwoActors(5, 5, 1, 1);
        var i = new Intention("player", "move", new Target(Position: new Position(5, 4)));
        Assert.True(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_rejects_move_two_tiles_away()
    {
        var s = TwoActors(5, 5, 1, 1);
        var i = new Intention("player", "move", new Target(Position: new Position(5, 3)));
        Assert.False(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_accepts_attack_by_actorId_when_adjacent()
    {
        var s = TwoActors(5, 5, 5, 4);
        var i = new Intention("player", "basic_attack", new Target(ActorId: "bot"));
        Assert.True(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_rejects_attack_by_actorId_when_not_adjacent()
    {
        var s = TwoActors(5, 5, 1, 1);
        var i = new Intention("player", "basic_attack", new Target(ActorId: "bot"));
        Assert.False(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_accepts_wait_with_no_target()
    {
        var s = TwoActors(5, 5, 1, 1);
        Assert.True(Rules.IsLegal(s, new Intention("player", "wait")));
    }

    [Fact]
    public void IsLegal_rejects_unknown_action()
    {
        var s = TwoActors(5, 5, 1, 1);
        Assert.False(Rules.IsLegal(s, new Intention("player", "teleport")));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: FAIL — `IsLegal` not defined.

- [ ] **Step 3: Implement IsLegal**

Add to `Core/Rules.cs` inside `class Rules`:
```csharp
    public static bool IsLegal(StateModel state, Intention intention)
    {
        var actor = state.ById(intention.ActorId);
        if (actor is null) return false;
        if (!Actions.All.TryGetValue(intention.ActionId, out var def)) return false;

        switch (def.TargetType)
        {
            case "none":
                return true;

            case "self":
                return intention.Target?.Self == true;

            case "tile":
            case "actor_or_tile":
                var pos = ResolveTargetPosition(state, intention);
                if (pos is null) return false;
                return LegalTargets(state, intention.ActorId, intention.ActionId)
                    .Contains(pos);

            default:
                return false;
        }
    }

    // Turn an intention's target into a concrete board position, or null if it can't.
    static Position? ResolveTargetPosition(StateModel state, Intention intention)
    {
        var t = intention.Target;
        if (t is null) return null;
        if (t.Position is not null) return t.Position;
        if (t.ActorId is not null)
        {
            var target = state.ById(t.ActorId);
            return target is null ? null : new Position(target.X, target.Y);
        }
        return null;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: PASS (11 passed).

- [ ] **Step 5: Commit**

```bash
git add Core/Rules.cs Core.Tests/RulesTests.cs
git commit -m "feat(core): server-side intention validation"
```

---

### Task 4: Core.Rules.Resolve — movement phase with conflict rules

**Files:**
- Modify: `Core/Rules.cs`
- Modify: `Core.Tests/RulesTests.cs`

**Interfaces:**
- Consumes: `StateModel`, `Intention`, `GameEvent`, `ResolveTargetPosition`.
- Produces: `static (StateModel state, List<GameEvent> events) Resolve(StateModel state, IReadOnlyDictionary<string, Intention> intentions)`.

This task implements movement + conflict rules + bump events. Attacks are added in Task 5 (Resolve will ignore non-move intentions until then; the attack tests live in Task 5).

- [ ] **Step 1: Write the failing tests**

Append to `Core.Tests/RulesTests.cs`:
```csharp
    static Dictionary<string, Intention> Intents(params Intention[] xs)
        => xs.ToDictionary(i => i.ActorId);

    [Fact]
    public void Resolve_applies_a_clean_move()
    {
        var s = TwoActors(5, 5, 1, 1);
        var (ns, _) = Rules.Resolve(s, Intents(
            new Intention("player", "move", new Target(Position: new Position(5, 4))),
            new Intention("bot", "wait")));
        var p = ns.ById("player")!;
        Assert.Equal(5, p.X);
        Assert.Equal(4, p.Y);
    }

    [Fact]
    public void Resolve_contested_tile_blocks_all_and_emits_bump_events()
    {
        // player at (5,5) and bot at (5,3) both move to (5,4).
        var s = TwoActors(5, 5, 5, 3);
        var (ns, events) = Rules.Resolve(s, Intents(
            new Intention("player", "move", new Target(Position: new Position(5, 4))),
            new Intention("bot", "move", new Target(Position: new Position(5, 4)))));
        Assert.Equal((5, 5), (ns.ById("player")!.X, ns.ById("player")!.Y)); // stayed
        Assert.Equal((5, 3), (ns.ById("bot")!.X, ns.ById("bot")!.Y));       // stayed
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "player" && e.TargetId == "bot");
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "bot" && e.TargetId == "player");
    }

    [Fact]
    public void Resolve_direct_swap_blocks_both_and_emits_bump_events()
    {
        // adjacent actors swapping: player (5,5)->(5,4), bot (5,4)->(5,5).
        var s = TwoActors(5, 5, 5, 4);
        var (ns, events) = Rules.Resolve(s, Intents(
            new Intention("player", "move", new Target(Position: new Position(5, 4))),
            new Intention("bot", "move", new Target(Position: new Position(5, 5)))));
        Assert.Equal((5, 5), (ns.ById("player")!.X, ns.ById("player")!.Y));
        Assert.Equal((5, 4), (ns.ById("bot")!.X, ns.ById("bot")!.Y));
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "player" && e.TargetId == "bot");
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "bot" && e.TargetId == "player");
    }

    [Fact]
    public void Resolve_missing_intention_defaults_to_wait()
    {
        var s = TwoActors(5, 5, 1, 1);
        var (ns, _) = Rules.Resolve(s, Intents()); // nobody submitted
        Assert.Equal((5, 5), (ns.ById("player")!.X, ns.ById("player")!.Y));
        Assert.Equal((1, 1), (ns.ById("bot")!.X, ns.ById("bot")!.Y));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: FAIL — `Resolve` not defined.

- [ ] **Step 3: Implement Resolve (movement only for now)**

Add to `Core/Rules.cs` inside `class Rules`:
```csharp
    const int MeleeBumpDamage = 1;

    public static (StateModel state, List<GameEvent> events) Resolve(
        StateModel state, IReadOnlyDictionary<string, Intention> intentions)
    {
        var next = state.Clone();
        var events = new List<GameEvent>();

        // Desired destination per actor that submitted a (legal) move.
        var desired = new Dictionary<string, Position>();
        foreach (var actor in next.Actors)
        {
            if (!intentions.TryGetValue(actor.Id, out var intent)) continue;       // missing -> wait
            if (intent.ActionId != "move") continue;
            if (!IsLegal(state, intent)) continue;                                  // invalid -> wait
            var pos = ResolveTargetPosition(state, intent);
            if (pos is not null) desired[actor.Id] = pos;
        }

        var blocked = new HashSet<string>();

        // contested_tile_blocks_all: 2+ actors targeting the same tile all stay put.
        foreach (var group in desired.GroupBy(kv => kv.Value).Where(g => g.Count() > 1))
        {
            var ids = group.Select(kv => kv.Key).ToList();
            foreach (var id in ids) blocked.Add(id);
            EmitBumpsAmong(events, ids);
        }

        // direct_swaps_blocked: A->B's tile and B->A's tile.
        foreach (var a in desired.Keys.ToList())
        {
            foreach (var b in desired.Keys.ToList())
            {
                if (string.CompareOrdinal(a, b) >= 0) continue;
                var aStart = state.ById(a)!; var bStart = state.ById(b)!;
                if (desired[a] == new Position(bStart.X, bStart.Y) &&
                    desired[b] == new Position(aStart.X, aStart.Y))
                {
                    blocked.Add(a); blocked.Add(b);
                    EmitBumpsAmong(events, new[] { a, b });
                }
            }
        }

        // Apply surviving moves.
        foreach (var (id, pos) in desired)
        {
            if (blocked.Contains(id)) continue;
            var actor = next.ById(id)!;
            actor.X = pos.X; actor.Y = pos.Y;
        }

        return (next, events);
    }

    static void EmitBumpsAmong(List<GameEvent> events, IReadOnlyList<string> ids)
    {
        // Each bumped actor trades a melee hit with every other actor in the bump.
        for (int i = 0; i < ids.Count; i++)
            for (int j = 0; j < ids.Count; j++)
                if (i != j)
                    events.Add(new GameEvent("damage", ids[i], ids[j], MeleeBumpDamage));
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: PASS (15 passed).

- [ ] **Step 5: Commit**

```bash
git add Core/Rules.cs Core.Tests/RulesTests.cs
git commit -m "feat(core): movement phase with contested-tile and swap conflict rules"
```

---

### Task 5: Core.Rules.Resolve — attacks phase (inert damage events)

**Files:**
- Modify: `Core/Rules.cs`
- Modify: `Core.Tests/RulesTests.cs`

**Interfaces:**
- Consumes: everything from Task 4.
- Produces: attack handling inside `Resolve` (no signature change). Emits `damage` events against post-movement positions; never changes `hp`.

- [ ] **Step 1: Write the failing tests**

Append to `Core.Tests/RulesTests.cs`:
```csharp
    [Fact]
    public void Resolve_attack_by_actorId_emits_inert_damage_event()
    {
        var s = TwoActors(5, 5, 5, 4);
        var (ns, events) = Rules.Resolve(s, Intents(
            new Intention("player", "basic_attack", new Target(ActorId: "bot")),
            new Intention("bot", "wait")));
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "player" && e.TargetId == "bot");
        Assert.Equal(30, ns.ById("bot")!.Hp); // inert: hp unchanged
    }

    [Fact]
    public void Resolve_attack_uses_post_movement_position()
    {
        // bot starts at (5,3) and moves to (5,4); player attacks tile (5,4).
        var s = TwoActors(5, 5, 5, 3);
        var (_, events) = Rules.Resolve(s, Intents(
            new Intention("player", "basic_attack", new Target(Position: new Position(5, 4))),
            new Intention("bot", "move", new Target(Position: new Position(5, 4)))));
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "player" && e.TargetId == "bot");
    }

    [Fact]
    public void Resolve_attack_on_empty_tile_emits_event_with_no_target()
    {
        var s = TwoActors(5, 5, 1, 1);
        var (_, events) = Rules.Resolve(s, Intents(
            new Intention("player", "basic_attack", new Target(Position: new Position(5, 4))),
            new Intention("bot", "wait")));
        Assert.Contains(events, e => e.Type == "damage" && e.SourceId == "player" && e.TargetId == null);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: FAIL — no attack events emitted yet.

- [ ] **Step 3: Implement the attacks phase**

In `Core/Rules.cs`, add the attacks loop in `Resolve` immediately before `return (next, events);`:
```csharp
        // Attacks phase: resolve against POST-movement positions. Inert (no hp change).
        const int MeleeAttackDamage = 1;
        foreach (var actor in next.Actors)
        {
            if (!intentions.TryGetValue(actor.Id, out var intent)) continue;
            if (intent.ActionId != "basic_attack") continue;
            if (!IsLegal(state, intent)) continue; // legality is judged on pre-move state, like submission

            // Find the targeted tile and whoever stands there now (post-movement).
            Position? tile = intent.Target?.Position;
            if (tile is null && intent.Target?.ActorId is not null)
            {
                var named = next.ById(intent.Target.ActorId);
                if (named is not null) tile = new Position(named.X, named.Y);
            }
            string? victimId = tile is null ? null : next.ActorAt(tile.X, tile.Y)?.Id;
            events.Add(new GameEvent("damage", actor.Id, victimId, MeleeAttackDamage));
        }

        // Cleanup phase: reserved (no deaths/status this increment).
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `$DOTNET test Core.Tests/Core.Tests.csproj`
Expected: PASS (18 passed).

- [ ] **Step 5: Commit**

```bash
git add Core/Rules.cs Core.Tests/RulesTests.cs
git commit -m "feat(core): inert attacks phase resolving against post-movement positions"
```

---

### Task 6: Server World — authoritative state, pending intentions, tick orchestration

**Files:**
- Modify: `Server/Server.csproj` (add Core reference)
- Modify: `Server/Game/World.cs` (rewrite around Core)
- Delete: `Server/Game/Entity.cs` (replaced by `ArenaGame.Core.ActorState`)

**Interfaces:**
- Consumes: `Core.Rules`, `Core.StateModel`, `Core.Actions`, contract DTOs.
- Produces, on `World`:
  - `ObservationDto Snapshot()` — the player's observation.
  - `bool Submit(Intention intention)` — validates with `Rules.IsLegal`; stores pending; triggers resolution when all actors pending; returns false if illegal.

- [ ] **Step 1: Reference Core from the server**

Add to `Server/Server.csproj` inside a `<ItemGroup>`:
```xml
  <ItemGroup>
    <ProjectReference Include="../Core/Core.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Rewrite World around Core**

Replace `Server/Game/World.cs` entirely:
```csharp
using ArenaGame.Core;

namespace ArenaGame.Game;

// Authoritative game state for one match. Holds the Core StateModel plus the
// turn bookkeeping (tick, pending intentions, last result). All rules live in Core.
public class World
{
    private readonly object _lock = new();
    private readonly Random _rng = new();

    private readonly StateModel _state;
    private readonly Dictionary<string, Intention> _pending = new();
    private int _tick;
    private LastTurnResultDto? _last;

    private const string PlayerId = "player";
    private const string BotId = "bot";

    private static readonly RulesDto RulesInfo = new(
        PhaseOrder: new[] { "movement", "attacks", "cleanup" },
        MovementConflictRule: "contested_tile_blocks_all",
        SwapRule: "direct_swaps_blocked",
        MissingAction: "wait");

    public World(int width, int height)
    {
        _state = new StateModel
        {
            Width = width, Height = height,
            Actors =
            {
                new ActorState { Id = PlayerId, Type = "player", TeamId = "players",
                                 Hp = 100, MaxHp = 100, X = width / 2, Y = height / 2 },
                new ActorState { Id = BotId, Type = "bot", TeamId = "bots",
                                 Hp = 30, MaxHp = 30, X = 1, Y = 1 },
            }
        };
        AutoPickBot();
    }

    public bool Submit(Intention intention)
    {
        lock (_lock)
        {
            if (intention.ActorId != PlayerId) return false;          // only the human posts
            if (!Rules.IsLegal(_state, intention)) return false;
            _pending[PlayerId] = intention;
            TryResolve();
            return true;
        }
    }

    // Choose a legal action for the bot for the current tick. Random move, else wait.
    private void AutoPickBot()
    {
        var moves = Rules.LegalTargets(_state, BotId, "move");
        _pending[BotId] = moves.Count > 0
            ? new Intention(BotId, "move", new Target(Position: moves[_rng.Next(moves.Count)]))
            : new Intention(BotId, "wait");
    }

    private void TryResolve()
    {
        if (!_state.Actors.All(a => _pending.ContainsKey(a.Id))) return;

        var submittedByPlayer = _pending.TryGetValue(PlayerId, out var pv) ? pv : null;
        var (next, events) = Rules.Resolve(_state, _pending);

        // Copy resolved positions back into the authoritative state.
        foreach (var a in _state.Actors)
        {
            var n = next.ById(a.Id)!;
            a.X = n.X; a.Y = n.Y; a.Hp = n.Hp;
        }

        _tick++;
        _last = new LastTurnResultDto(_tick, submittedByPlayer, true, events.ToArray());
        _pending.Clear();
        AutoPickBot(); // bot is ready again for the next tick
    }

    public ObservationDto Snapshot()
    {
        lock (_lock)
        {
            var self = _state.ById(PlayerId)!;
            return new ObservationDto(
                Tick: _tick,
                Width: _state.Width,
                Height: _state.Height,
                Rules: RulesInfo,
                Self: new SelfDto(self.Id, self.Type, self.TeamId, self.Hp, self.MaxHp,
                                  new Position(self.X, self.Y), self.StatusEffects),
                VisibleActors: _state.Actors.Where(a => a.Id != PlayerId)
                    .Select(a => new ActorDto(a.Id, a.Type, a.TeamId, a.Hp, a.MaxHp,
                                              new Position(a.X, a.Y), a.StatusEffects))
                    .ToArray(),
                VisibleTiles: AllTiles(),
                Auras: Array.Empty<object>(),
                AvailableActions: Actions.OfferedFor(_state, PlayerId),
                LastTurnResult: _last,
                PlayerSubmitted: _pending.ContainsKey(PlayerId));
        }
    }

    private TileDto[] AllTiles()
    {
        var tiles = new List<TileDto>(_state.Width * _state.Height);
        for (int y = 0; y < _state.Height; y++)
            for (int x = 0; x < _state.Width; x++)
                tiles.Add(new TileDto(x, y, "stone", false));
        return tiles.ToArray();
    }
}
```

- [ ] **Step 3: Delete the obsolete Entity type**

```bash
rm Server/Game/Entity.cs
```

- [ ] **Step 4: Build the server**

Run: `$DOTNET build Server/Server.csproj`
Expected: FAIL — `Program.cs` still references the old `world.Apply` / `Snapshot()` shape and `ActionRequest`. That is fixed in Task 7. (If you are running tasks strictly one at a time, you may instead verify by building Core only here and defer the server build to Task 7.)

- [ ] **Step 5: Commit**

```bash
git add Server/Server.csproj Server/Game/World.cs
git commit -m "feat(server): rebuild World on Core with pending intentions and tick resolution"
```

---

### Task 7: Server endpoints — observation + intention, no wander loop

**Files:**
- Modify: `Server/Program.cs`

**Interfaces:**
- Consumes: `World.Snapshot()`, `World.Submit(Intention)`, `ArenaGame.Core.Intention`.
- Produces: `GET /state` → observation JSON; `POST /action` → 200 on accept, 400 on illegal.

- [ ] **Step 1: Rewrite Program.cs**

Replace `Server/Program.cs` entirely:
```csharp
using ArenaGame.Core;
using ArenaGame.Game;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// One shared world for this POC.
var world = new World(width: 12, height: 12);

// Turn-based now: no background tick loop. The world resolves a tick when every
// actor has a pending intention (the bot auto-readies; the human POSTs /action).

// Client polls this to render (observation for the player's perspective).
app.MapGet("/state", () => Results.Json(world.Snapshot()));

// Human submits an intention. Server is the authority: illegal -> 400.
app.MapPost("/action", (Intention intention) =>
    world.Submit(intention) ? Results.Ok() : Results.BadRequest(new { error = "illegal action" }));

app.Run("http://localhost:5000");
```

- [ ] **Step 2: Build the server**

Run: `$DOTNET build Server/Server.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Manually verify the protocol (Windows shell)**

Start the server (`$DOTNET run --project Server`), then from a second shell:
```bash
curl -s http://localhost:5000/state
# Expect JSON with tick, self, visibleActors, availableActions (move/basic_attack/wait), playerSubmitted:false.
curl -s -X POST http://localhost:5000/action -H "Content-Type: application/json" \
  -d '{"actorId":"player","actionId":"move","target":{"position":{"x":6,"y":5}}}'
# Expect HTTP 200; a follow-up /state shows the player moved and tick incremented.
curl -s -X POST http://localhost:5000/action -H "Content-Type: application/json" \
  -d '{"actorId":"player","actionId":"move","target":{"position":{"x":0,"y":0}}}'
# Expect HTTP 400 (illegal: not adjacent).
```

- [ ] **Step 4: Commit**

```bash
git add Server/Program.cs
git commit -m "feat(server): turn-based observation/intention endpoints, remove wander loop"
```

---

### Task 8: Client — reference Core, render observation + action menu

**Files:**
- Modify: `Client/Client.csproj` (add Core reference)
- Modify: `Client/Program.cs` (replace local DTOs with Core; render menu)

**Interfaces:**
- Consumes: `ObservationDto`, `Actions`, `StateModel.FromObservation`.
- Produces (within `Program`): `static ObservationDto? SnapshotState()`; menu rendering driven by `obs.AvailableActions`; UI mode scaffolding `enum Mode { Menu, Targeting, Waiting }`.

This task replaces the old `StateDto`/`EntityDto`/arrow-key movement with the observation model and a clickable menu, but does NOT yet wire targeting (Task 9) or submission (Task 10). After this task the window shows the board and a menu; clicking a menu item only changes local mode.

- [ ] **Step 1: Reference Core from the client**

Add to `Client/Client.csproj` inside an `<ItemGroup>`:
```xml
  <ItemGroup>
    <ProjectReference Include="../Core/Core.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Replace DTOs and polling, add menu rendering**

In `Client/Program.cs`:

Replace the top-of-file local DTO records (the `record StateDto...`, `record EntityDto...`, `record ActionRequest...` lines) with:
```csharp
using ArenaGame.Core;
```
(keep the existing `using System.Net.Http.Json;`, `using System.Text.Json;`, `using Raylib_cs;`).

Replace the `_latestState` field type and `SnapshotState`/`PollLoop`/`HandleInput`/`SendAction` and rendering helpers so the client speaks the observation model. Concretely:

- Change the cached field and accessor:
```csharp
    static ObservationDto? _latestState;

    static ObservationDto? SnapshotState()
    {
        lock (StateLock) return _latestState;
    }
```
- Change `PollLoop` to fetch the observation:
```csharp
    static async Task PollLoop()
    {
        while (true)
        {
            try
            {
                var obs = await Http.GetFromJsonAsync<ObservationDto>("/state", JsonOpts);
                if (obs is not null) lock (StateLock) _latestState = obs;
            }
            catch { /* server starting up; retry */ }
            await Task.Delay(100);
        }
    }
```
- Add UI mode state near the other static fields:
```csharp
    enum Mode { Menu, Targeting, Waiting }
    static Mode _mode = Mode.Menu;
    static string? _selectedActionId;
    const int MenuHeight = 96;
```
- In `Main`, size the window from the observation and add room for the menu. Replace the `gridW`/`gridH`/`InitWindow` lines with:
```csharp
        ObservationDto? initial = SnapshotState();
        int gridW = initial?.Width ?? 12;
        int gridH = initial?.Height ?? 12;

        Raylib.InitWindow(gridW * TileSize, gridH * TileSize + MenuHeight, "Arena Game");
        Raylib.SetTargetFPS(60);
```
- In the render loop, drop the old window-resize/`HandleInput` arrow-key block. Replace the body that draws when `state is not null` with:
```csharp
                DrawFloor(state, floorTex);
                DrawEntities(state, playerTex, botTex);
                DrawMenu(state);
```
  and update `DrawFloor`/`DrawEntities` calls to use `state.Width`/`state.Height` and `state.VisibleActors` + `state.Self` (see Step 3).

- [ ] **Step 3: Update rendering helpers for the observation model**

Replace `DrawFloor`, `DrawEntities`, and `DrawLegend` with:
```csharp
    static void DrawFloor(ObservationDto s, Texture2D floorTex)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                int px = x * TileSize, py = y * TileSize;
                if (floorTex.Id != 0) DrawTextureScaled(floorTex, px, py);
                else Raylib.DrawRectangle(px, py, TileSize, TileSize, new Color(30, 30, 35, 255));
                Raylib.DrawRectangleLines(px, py, TileSize, TileSize, new Color(60, 60, 70, 255));
            }
    }

    static void DrawEntities(ObservationDto s, Texture2D playerTex, Texture2D botTex)
    {
        DrawActor(s.Self.Position.X, s.Self.Position.Y, true, playerTex, botTex);
        foreach (var a in s.VisibleActors)
            DrawActor(a.Position.X, a.Position.Y, false, playerTex, botTex);
    }

    static void DrawActor(int x, int y, bool isPlayer, Texture2D playerTex, Texture2D botTex)
    {
        int px = x * TileSize, py = y * TileSize;
        Texture2D tex = isPlayer ? playerTex : botTex;
        if (tex.Id != 0) DrawTextureScaled(tex, px, py);
        else Raylib.DrawRectangle(px, py, TileSize, TileSize, isPlayer ? Color.Blue : Color.Red);
    }

    // Pokémon-style action menu along the bottom. Returns the menu-item rectangles
    // so input handling (Task 9/10) can hit-test clicks.
    static List<(Rectangle rect, ActionDef action)> DrawMenu(ObservationDto s)
    {
        var hits = new List<(Rectangle, ActionDef)>();
        int top = s.Height * TileSize;
        Raylib.DrawRectangle(0, top, s.Width * TileSize, MenuHeight, new Color(20, 20, 28, 255));

        int x = 12, y = top + 12, w = 150, h = 34, gap = 10;
        foreach (var a in s.AvailableActions)
        {
            var rect = new Rectangle(x, y, w, h);
            bool selected = a.Id == _selectedActionId;
            Raylib.DrawRectangleRec(rect, selected ? new Color(80, 80, 120, 255) : new Color(45, 45, 60, 255));
            Raylib.DrawRectangleLinesEx(rect, 1, new Color(120, 120, 150, 255));
            Raylib.DrawText(a.Id, (int)rect.X + 10, (int)rect.Y + 9, 18, Color.RayWhite);
            hits.Add((rect, a));
            x += w + gap;
            if (x + w > s.Width * TileSize) { x = 12; y += h + gap; }
        }

        string hint = _mode switch
        {
            Mode.Targeting => "Select a highlighted tile  (Esc / right-click to cancel)",
            Mode.Waiting   => "Waiting for turn to resolve...",
            _              => "Choose an action"
        };
        Raylib.DrawText(hint, 12, top + MenuHeight - 22, 16, new Color(170, 170, 190, 255));
        return hits;
    }
```

- [ ] **Step 4: Build the client**

Run: `$DOTNET build Client/Client.csproj`
Expected: `Build succeeded`. (Unused-field warnings for `_mode`/`_selectedActionId` are acceptable until Task 9 wires them.)

- [ ] **Step 5: Commit**

```bash
git add Client/Client.csproj Client/Program.cs
git commit -m "feat(client): consume observation model and render action menu"
```

---

### Task 9: Client — menu selection, targeting mode, tile highlighting, cancel

**Files:**
- Modify: `Client/Program.cs`

**Interfaces:**
- Consumes: `DrawMenu` hit rectangles, `Rules.LegalTargets`, `StateModel.FromObservation`.
- Produces (within `Program`): `static void HandleMouse(ObservationDto obs, List<(Rectangle rect, ActionDef action)> menuHits)`; `static List<Position> CurrentTargets(ObservationDto obs)`; highlight drawing.

- [ ] **Step 1: Wire the render loop to capture menu hits and draw highlights**

In `Main`'s render loop, change the draw block to keep the menu rectangles and draw highlights + handle mouse before `EndDrawing`:
```csharp
                DrawFloor(state, floorTex);
                DrawHighlights(state);                       // under entities
                DrawEntities(state, playerTex, botTex);
                var menuHits = DrawMenu(state);
                HandleMouse(state, menuHits);
```

- [ ] **Step 2: Add targeting + cancel + highlight logic**

Add to `Program`:
```csharp
    // Legal tiles for the currently-selected action, computed locally via Core
    // (the same function the server validates with). Empty when not targeting.
    static List<Position> CurrentTargets(ObservationDto obs)
    {
        if (_mode != Mode.Targeting || _selectedActionId is null) return new();
        var model = StateModel.FromObservation(obs);
        return Rules.LegalTargets(model, obs.Self.Id, _selectedActionId);
    }

    static void DrawHighlights(ObservationDto obs)
    {
        foreach (var p in CurrentTargets(obs))
        {
            int px = p.X * TileSize, py = p.Y * TileSize;
            Raylib.DrawRectangle(px, py, TileSize, TileSize, new Color(80, 200, 120, 90));
            Raylib.DrawRectangleLinesEx(new Rectangle(px, py, TileSize, TileSize), 2,
                new Color(120, 240, 160, 255));
        }
    }

    static void HandleMouse(ObservationDto obs, List<(Rectangle rect, ActionDef action)> menuHits)
    {
        // Cancel: Esc or right-click returns to the menu without spending the turn.
        if (_mode == Mode.Targeting &&
            (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsMouseButtonPressed(MouseButton.Right)))
        {
            _mode = Mode.Menu; _selectedActionId = null;
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();

        // Click on a menu item?
        foreach (var (rect, action) in menuHits)
        {
            if (!Raylib.CheckCollisionPointRec(m, rect)) continue;
            OnMenuClick(action);
            return;
        }

        // Click on the board while targeting?
        if (_mode == Mode.Targeting && _selectedActionId is not null)
        {
            int tx = (int)(m.X / TileSize), ty = (int)(m.Y / TileSize);
            if (CurrentTargets(obs).Contains(new Position(tx, ty)))
                CommitTarget(obs, tx, ty);   // implemented in Task 10
        }
    }

    static void OnMenuClick(ActionDef action)
    {
        if (_mode == Mode.Waiting) return;

        // Re-clicking the selected action (or clicking another) cancels targeting.
        if (_mode == Mode.Targeting && action.Id == _selectedActionId)
        {
            _mode = Mode.Menu; _selectedActionId = null;
            return;
        }

        _selectedActionId = action.Id;
        if (action.TargetType == "none" || action.TargetType == "self")
            CommitImmediate(action);         // implemented in Task 10
        else
            _mode = Mode.Targeting;
    }
```

- [ ] **Step 3: Add temporary no-op commit stubs so it builds**

Add these stubs (replaced with real submission in Task 10):
```csharp
    static void CommitImmediate(ActionDef action) { _mode = Mode.Menu; _selectedActionId = null; }
    static void CommitTarget(ObservationDto obs, int tx, int ty) { _mode = Mode.Menu; _selectedActionId = null; }
```

- [ ] **Step 4: Build the client**

Run: `$DOTNET build Client/Client.csproj`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add Client/Program.cs
git commit -m "feat(client): action selection, targeting highlights, and cancel"
```

---

### Task 10: Client — submit intentions and waiting/new-tick handling

**Files:**
- Modify: `Client/Program.cs`

**Interfaces:**
- Consumes: `Intention`, `Target`, server `POST /action`, `ObservationDto.Tick`.
- Produces: real `CommitImmediate`/`CommitTarget` that POST an `Intention`; tick-change detection that clears `Waiting` back to `Menu`.

- [ ] **Step 1: Track the tick we submitted on**

Add a static field:
```csharp
    static int? _submittedAtTick;
```

- [ ] **Step 2: Replace the commit stubs with real submission**

Replace the two stub methods from Task 9 with:
```csharp
    static void CommitImmediate(ObservationDto obs, ActionDef action)
    {
        Target? target = action.TargetType == "self" ? new Target(Self: true) : null;
        Submit(obs, new Intention(obs.Self.Id, action.Id, target));
    }

    static void CommitTarget(ObservationDto obs, int tx, int ty)
    {
        Submit(obs, new Intention(obs.Self.Id, _selectedActionId!, new Target(Position: new Position(tx, ty))));
    }

    static void Submit(ObservationDto obs, Intention intention)
    {
        _submittedAtTick = obs.Tick;
        _mode = Mode.Waiting;
        _selectedActionId = null;
        _ = SendIntention(intention);
    }

    static async Task SendIntention(Intention intention)
    {
        try { await Http.PostAsJsonAsync("/action", intention, JsonOpts); }
        catch { /* fire-and-forget; the Waiting state simply persists until next tick */ }
    }
```

- [ ] **Step 3: Fix the call site for CommitImmediate**

In `OnMenuClick`, change `CommitImmediate(action);` to `CommitImmediate(obs, action);` and give `OnMenuClick` access to `obs` by changing its signature to `static void OnMenuClick(ObservationDto obs, ActionDef action)` and updating the call in `HandleMouse` to `OnMenuClick(obs, action);`.

- [ ] **Step 4: Clear Waiting when the tick advances**

At the start of `HandleMouse`, before the cancel check, add:
```csharp
        // A new tick resolved: return control to the player.
        if (_mode == Mode.Waiting && _submittedAtTick is int t && obs.Tick > t)
        {
            _mode = Mode.Menu;
            _submittedAtTick = null;
        }
        if (_mode == Mode.Waiting) return; // ignore input while resolving
```

- [ ] **Step 5: Build the client**

Run: `$DOTNET build Client/Client.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 6: Build the whole solution**

Run: `$DOTNET build arena-game-csharp.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 7: Manual end-to-end verification (Windows, real display)**

Start server then client on Windows. Confirm:
- Menu shows `move`, `basic_attack`, `wait`.
- Clicking `move` highlights the up-to-4 adjacent tiles; clicking one moves the player and the bot also moves (tick advanced); menu re-enables.
- Right-click and Esc both cancel targeting; re-clicking the selected menu item cancels.
- Clicking `basic_attack` highlights adjacent tiles; clicking the bot's adjacent tile resolves (player stays/acts, tick advances).
- Clicking `wait` advances the tick with no move.
Report exactly what was observed; do not claim success without running it.

- [ ] **Step 8: Commit**

```bash
git add Client/Program.cs
git commit -m "feat(client): submit intentions and handle waiting/new-tick"
```

---

## Self-Review Notes

- **Spec coverage:** turn loop/simultaneous resolution (Task 6), phased resolution + conflict rules + bump events (Tasks 4–5), action descriptors with `targetType`/`range` (Task 1), intention reply shape (Tasks 1, 7, 10), shared Core with `LegalTargets`/`IsLegal`/`Resolve` (Tasks 1–5), client menu + targeting + highlight + cancel (Tasks 8–9), inert combat (Task 5), `healing_potion` defined-not-offered (Task 1), whole-board `visibleTiles` (Task 6), `playerSubmitted` + tick-change clears selection (Tasks 6, 10). Deferred items (fog, auras, inventory, real damage) are intentionally absent.
- **Type consistency:** `Intention(ActorId, ActionId, Target?)`, `Target(Position?, ActorId?, Self)`, `Resolve(StateModel, IReadOnlyDictionary<string,Intention>) -> (StateModel, List<GameEvent>)`, `LegalTargets(...) -> List<Position>`, `IsLegal(StateModel, Intention) -> bool` are used identically across server, client, and tests.
- **Ordering caveat:** Task 6 leaves the server temporarily non-building (Program.cs updated in Task 7); both build cleanly by end of Task 7. Flagged in Task 6 Step 4.
