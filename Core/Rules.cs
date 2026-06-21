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
}
