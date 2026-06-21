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
