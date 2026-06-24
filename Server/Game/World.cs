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
    private readonly HashSet<(int X, int Y)> _boulders = new(); // blocked tiles that are boulders, not border walls
    private int _tick;
    private LastTurnResultDto? _last;

    private const string PlayerId = "player";
    private const string BotId = "bot";

    private static readonly RulesDto RulesInfo = new(
        PhaseOrder: new[] { "movement", "attacks", "cleanup" },
        MovementConflictRule: "contested_tile_blocks_all",
        SwapRule: "direct_swaps_blocked",
        MissingAction: ActionIds.Wait);

    public World(int width, int height)
    {
        _state = new StateModel
        {
            Width = width, Height = height,
            Actors =
            {
                new ActorState { Id = PlayerId, Type = ActorTypes.Player, TeamId = "players",
                                 Hp = 10, MaxHp = 10, X = width / 2, Y = height / 2 },
                new ActorState { Id = BotId, Type = ActorTypes.Bot, TeamId = "bots",
                                 Hp = 10, MaxHp = 10, X = 1, Y = 1 },
            }
        };
        GenerateObstacles();
        AutoPickBot();
    }

    // Border wall around the arena + boulders scattered on ~5-10% of the board.
    // Actor start tiles are kept clear.
    private void GenerateObstacles()
    {
        int w = _state.Width, h = _state.Height;
        var occupied = _state.Actors.Select(a => (a.X, a.Y)).ToHashSet();

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                    _state.Blocked.Add((x, y));               // border wall

        int target = (int)(w * h * (0.05 + _rng.NextDouble() * 0.05)); // 5-10% boulders
        int placed = 0, guard = 0;
        while (placed < target && guard++ < w * h * 10)
        {
            int bx = _rng.Next(1, w - 1), by = _rng.Next(1, h - 1);
            var cell = (bx, by);
            if (occupied.Contains(cell) || _state.Blocked.Contains(cell)) continue;
            _state.Blocked.Add(cell);
            _boulders.Add(cell);
            placed++;
        }
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
        var moves = Rules.LegalTargets(_state, BotId, ActionIds.Move);
        _pending[BotId] = moves.Count > 0
            ? new Intention(BotId, ActionIds.Move, new Target(Position: moves[_rng.Next(moves.Count)]))
            : new Intention(BotId, ActionIds.Wait);
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
            {
                bool blocked = _state.IsBlocked(x, y);
                string terrain = !blocked ? Terrains.Stone : _boulders.Contains((x, y)) ? Terrains.Boulder : Terrains.Wall;
                tiles.Add(new TileDto(x, y, terrain, blocked));
            }
        return tiles.ToArray();
    }
}
