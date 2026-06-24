namespace ArenaGame.Core;

public record ActionDef(string Id, string Source, string TargetType, int Range, string[] Tags);

public static class Actions
{
    public static readonly IReadOnlyDictionary<string, ActionDef> All = new Dictionary<string, ActionDef>
    {
        [ActionIds.Move]          = new(ActionIds.Move,          "system",    TargetTypes.Tile,        1, new[] { "movement" }),
        [ActionIds.BasicAttack]   = new(ActionIds.BasicAttack,   "ability",   TargetTypes.ActorOrTile, 1, new[] { "melee", "damage" }),
        [ActionIds.Wait]          = new(ActionIds.Wait,          "system",    TargetTypes.None,        0, Array.Empty<string>()),
        // Defined but NOT offered this increment (proves the menu is data-driven).
        [ActionIds.HealingPotion] = new(ActionIds.HealingPotion, "inventory", TargetTypes.Self,        0, new[] { "heal" }),
    };

    // The ids offered to actors this increment, in menu order.
    public static readonly string[] OfferedIds = { ActionIds.Move, ActionIds.BasicAttack, ActionIds.Wait };

    public static ActionDef[] OfferedFor(StateModel state, string actorId)
        => OfferedIds.Select(id => All[id]).ToArray();
}
