namespace ArenaGame.Game;

// The whole game lives here. It knows nothing about HTTP, JSON, or rendering —
// it just holds state and applies actions. Everything else is an adapter around it.
public class World
{
    private readonly object _lock = new();
    private readonly Random _rng = new();

    public int Width { get; }
    public int Height { get; }
    private readonly List<Entity> _entities = new();

    // The fixed menu of actions the server offers clients. The client renders
    // and chooses from THIS list rather than hardcoding its own — the server is
    // the authority on what's legal. Grows over time (Attack, Bank, ...).
    public static readonly string[] AvailableActions =
    {
        "MoveUp", "MoveDown", "MoveLeft", "MoveRight",
    };

    public World(int width, int height)
    {
        Width = width;
        Height = height;
        _entities.Add(new Entity { Id = "player", Kind = "player", X = width / 2, Y = height / 2 });
        _entities.Add(new Entity { Id = "bot", Kind = "bot", X = 1, Y = 1 });
    }

    // The state -> action boundary. Apply one action for one entity.
    // Action "type" is a string from a predefined list; that's what a remote
    // bot or human client will send as JSON later.
    public void Apply(string entityId, string actionType)
    {
        lock (_lock)
        {
            var e = _entities.FirstOrDefault(x => x.Id == entityId);
            if (e is null) return;

            var (dx, dy) = actionType switch
            {
                "MoveUp" => (0, -1),
                "MoveDown" => (0, 1),
                "MoveLeft" => (-1, 0),
                "MoveRight" => (1, 0),
                _ => (0, 0),
            };
            MoveWithinBounds(e, dx, dy);
        }
    }

    // Server-driven tick: bots that aren't controlled by an external responder
    // do something on their own. For now the bot just wanders.
    public void Step()
    {
        lock (_lock)
        {
            var bot = _entities.First(e => e.Id == "bot");
            var (dx, dy) = _rng.Next(4) switch
            {
                0 => (0, -1),
                1 => (0, 1),
                2 => (-1, 0),
                _ => (1, 0),
            };
            MoveWithinBounds(bot, dx, dy);
        }
    }

    private void MoveWithinBounds(Entity e, int dx, int dy)
    {
        int nx = Math.Clamp(e.X + dx, 0, Width - 1);
        int ny = Math.Clamp(e.Y + dy, 0, Height - 1);
        e.X = nx;
        e.Y = ny;
    }

    // Read-only snapshot for the client. This is the shape that becomes the JSON payload.
    public object Snapshot()
    {
        lock (_lock)
        {
            return new
            {
                width = Width,
                height = Height,
                actions = AvailableActions,
                entities = _entities
                    .Select(e => new { id = e.Id, kind = e.Kind, x = e.X, y = e.Y })
                    .ToArray(),
            };
        }
    }
}
