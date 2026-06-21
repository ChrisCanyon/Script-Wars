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
