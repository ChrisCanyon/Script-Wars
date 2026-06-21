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
