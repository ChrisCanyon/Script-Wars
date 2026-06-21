namespace ArenaGame.Core;

public class ActorState
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string TeamId { get; init; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string[] StatusEffects { get; set; } = Array.Empty<string>();

    public ActorState Clone() => new()
    {
        Id = Id, Type = Type, TeamId = TeamId, Hp = Hp, MaxHp = MaxHp,
        X = X, Y = Y, StatusEffects = (string[])StatusEffects.Clone()
    };
}

public class StateModel
{
    public int Width { get; init; }
    public int Height { get; init; }
    public List<ActorState> Actors { get; init; } = new();

    public ActorState? ById(string id) => Actors.FirstOrDefault(a => a.Id == id);
    public ActorState? ActorAt(int x, int y) => Actors.FirstOrDefault(a => a.X == x && a.Y == y);
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public StateModel Clone() => new()
    {
        Width = Width, Height = Height,
        Actors = Actors.Select(a => a.Clone()).ToList()
    };

    // Build a rules-ready snapshot from a received observation (used by the client).
    public static StateModel FromObservation(ObservationDto obs)
    {
        var actors = new List<ActorState>
        {
            new() { Id = obs.Self.Id, Type = obs.Self.Type, TeamId = obs.Self.TeamId,
                    Hp = obs.Self.Hp, MaxHp = obs.Self.MaxHp,
                    X = obs.Self.Position.X, Y = obs.Self.Position.Y,
                    StatusEffects = obs.Self.StatusEffects }
        };
        actors.AddRange(obs.VisibleActors.Select(a => new ActorState
        {
            Id = a.Id, Type = a.Type, TeamId = a.TeamId, Hp = a.Hp, MaxHp = a.MaxHp,
            X = a.Position.X, Y = a.Position.Y, StatusEffects = a.StatusEffects
        }));
        return new StateModel { Width = obs.Width, Height = obs.Height, Actors = actors };
    }
}
