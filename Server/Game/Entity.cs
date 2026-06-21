namespace ArenaGame.Game;

public class Entity
{
    public required string Id { get; init; }
    public required string Kind { get; init; }  // "player" | "bot" — drives sprite color client-side
    public int X { get; set; }
    public int Y { get; set; }
}
