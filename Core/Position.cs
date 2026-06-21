namespace ArenaGame.Core;

public record Position(int X, int Y);

// Emitted by Resolve. Inert this increment (Amount is informational; hp is unchanged).
public record GameEvent(string Type, string SourceId, string? TargetId, int Amount);
