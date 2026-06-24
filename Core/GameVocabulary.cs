namespace ArenaGame.Core;

// Canonical wire-contract tokens shared by client and server. These exact strings travel
// as JSON (action ids, target types, event types, terrains, actor types) and the action
// registry is keyed by them, so they are string constants — not an enum — to round-trip
// verbatim. Reference these instead of repeating the literals.

public static class ActionIds
{
    public const string Move = "move";
    public const string BasicAttack = "basic_attack";
    public const string Wait = "wait";
    public const string HealingPotion = "healing_potion";
}

public static class TargetTypes
{
    public const string Tile = "tile";
    public const string ActorOrTile = "actor_or_tile";
    public const string Self = "self";
    public const string None = "none";
}

public static class EventTypes
{
    public const string Damage = "damage";
}

public static class Terrains
{
    public const string Stone = "stone";
    public const string Wall = "wall";
    public const string Boulder = "boulder";
}

public static class ActorTypes
{
    public const string Player = "player";
    public const string Bot = "bot";
}
