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
            case TargetTypes.Tile: // movement: in-bounds, not blocked, unoccupied, within range (range 1 = adjacent)
                foreach (var (dx, dy) in Adjacent)
                {
                    int nx = actor.X + dx, ny = actor.Y + dy;
                    if (state.InBounds(nx, ny) && !state.IsBlocked(nx, ny) && state.ActorAt(nx, ny) is null)
                        result.Add(new Position(nx, ny));
                }
                break;

            case TargetTypes.ActorOrTile: // attack: any in-bounds adjacent tile (occupied or not)
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
            case TargetTypes.None:
                return true;

            case TargetTypes.Self:
                return intention.Target?.Self == true;

            case TargetTypes.Tile:
            case TargetTypes.ActorOrTile:
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

    const int MeleeBumpDamage = 1;

    public static (StateModel state, List<GameEvent> events) Resolve(
        StateModel state, IReadOnlyDictionary<string, Intention> intentions)
    {
        var next = state.Clone();
        var events = new List<GameEvent>();

        // Desired destination per actor that submitted a move.
        // We check adjacency + in-bounds but NOT occupancy (conflict resolution handles that).
        var desired = new Dictionary<string, Position>();
        foreach (var actor in next.Actors)
        {
            if (!intentions.TryGetValue(actor.Id, out var intent)) continue;       // missing -> wait
            if (intent.ActionId != ActionIds.Move) continue;
            var pos = ResolveTargetPosition(state, intent);
            if (pos is null) continue;
            // Must be in-bounds, not a blocked tile, and adjacent (range-1 move).
            int dx = Math.Abs(pos.X - actor.X), dy = Math.Abs(pos.Y - actor.Y);
            bool isAdjacent = (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
            if (!state.InBounds(pos.X, pos.Y) || state.IsBlocked(pos.X, pos.Y) || !isAdjacent)
                continue;                                                         // invalid -> wait
            desired[actor.Id] = pos;
        }

        var blocked = new HashSet<string>();

        // Block moves into tiles occupied by actors that are NOT moving away.
        // The mover bumps the stationary occupant: both trade a melee hit (inert this increment).
        foreach (var (id, pos) in desired)
        {
            var occupant = state.ActorAt(pos.X, pos.Y);
            if (occupant is not null && !desired.ContainsKey(occupant.Id))
            {
                blocked.Add(id);
                EmitBumpsAmong(events, new[] { id, occupant.Id });
            }
        }

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

        // Attacks phase: resolve against POST-movement positions; the cleanup phase
        // applies the resulting damage events to hp.
        const int MeleeAttackDamage = 1;
        foreach (var actor in next.Actors)
        {
            if (!intentions.TryGetValue(actor.Id, out var intent)) continue;
            if (intent.ActionId != ActionIds.BasicAttack) continue;
            if (!IsLegal(state, intent)) continue; // legality is judged on pre-move state, like submission

            // Find the targeted tile and whoever stands there now (post-movement).
            Position? tile = intent.Target?.Position;
            if (tile is null && intent.Target?.ActorId is not null)
            {
                var named = next.ById(intent.Target.ActorId);
                if (named is not null) tile = new Position(named.X, named.Y);
            }
            string? victimId = tile is null ? null : next.ActorAt(tile.X, tile.Y)?.Id;
            events.Add(new GameEvent(EventTypes.Damage, actor.Id, victimId, MeleeAttackDamage));
        }

        // Cleanup phase: apply every damage event to the victim's hp, clamped at 0.
        // Both attacks and collision bumps deal damage. No death/removal yet.
        foreach (var ev in events)
        {
            if (ev.Type != EventTypes.Damage || ev.TargetId is null) continue;
            var victim = next.ById(ev.TargetId);
            if (victim is not null) victim.Hp = Math.Max(0, victim.Hp - ev.Amount);
        }

        return (next, events);
    }

    static void EmitBumpsAmong(List<GameEvent> events, IReadOnlyList<string> ids)
    {
        // Each bumped actor trades a melee hit with every other actor in the bump.
        for (int i = 0; i < ids.Count; i++)
            for (int j = 0; j < ids.Count; j++)
                if (i != j)
                    events.Add(new GameEvent(EventTypes.Damage, ids[i], ids[j], MeleeBumpDamage));
    }
}
