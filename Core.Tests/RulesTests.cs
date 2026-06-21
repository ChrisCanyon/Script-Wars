using ArenaGame.Core;
using Xunit;

public class RulesTests
{
    static StateModel TwoActors(int px, int py, int bx, int by, int w = 12, int h = 12) => new()
    {
        Width = w, Height = h,
        Actors =
        {
            new ActorState { Id = "player", Type = "player", TeamId = "players", Hp = 100, MaxHp = 100, X = px, Y = py },
            new ActorState { Id = "bot",    Type = "bot",    TeamId = "bots",    Hp = 30,  MaxHp = 30,  X = bx, Y = by },
        }
    };

    [Fact]
    public void Move_offers_four_adjacent_in_bounds_unoccupied_tiles()
    {
        var s = TwoActors(5, 5, 1, 1);
        var t = Rules.LegalTargets(s, "player", "move");
        Assert.Equal(4, t.Count);
        Assert.Contains(new Position(5, 4), t);
        Assert.Contains(new Position(5, 6), t);
        Assert.Contains(new Position(4, 5), t);
        Assert.Contains(new Position(6, 5), t);
    }

    [Fact]
    public void Move_excludes_off_board_tiles_at_corner()
    {
        var s = TwoActors(0, 0, 5, 5);
        var t = Rules.LegalTargets(s, "player", "move");
        Assert.Equal(2, t.Count); // only (1,0) and (0,1)
    }

    [Fact]
    public void Move_excludes_tile_occupied_by_another_actor()
    {
        var s = TwoActors(5, 5, 5, 4); // bot directly above player
        var t = Rules.LegalTargets(s, "player", "move");
        Assert.DoesNotContain(new Position(5, 4), t);
        Assert.Equal(3, t.Count);
    }

    [Fact]
    public void Attack_offers_adjacent_tiles_including_occupied()
    {
        var s = TwoActors(5, 5, 5, 4); // bot adjacent above
        var t = Rules.LegalTargets(s, "player", "basic_attack");
        Assert.Contains(new Position(5, 4), t); // can target the bot's tile
        Assert.Equal(4, t.Count);
    }

    [Fact]
    public void Wait_has_no_targets()
    {
        var s = TwoActors(5, 5, 1, 1);
        Assert.Empty(Rules.LegalTargets(s, "player", "wait"));
    }

    [Fact]
    public void IsLegal_accepts_move_to_adjacent_tile()
    {
        var s = TwoActors(5, 5, 1, 1);
        var i = new Intention("player", "move", new Target(Position: new Position(5, 4)));
        Assert.True(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_rejects_move_two_tiles_away()
    {
        var s = TwoActors(5, 5, 1, 1);
        var i = new Intention("player", "move", new Target(Position: new Position(5, 3)));
        Assert.False(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_accepts_attack_by_actorId_when_adjacent()
    {
        var s = TwoActors(5, 5, 5, 4);
        var i = new Intention("player", "basic_attack", new Target(ActorId: "bot"));
        Assert.True(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_rejects_attack_by_actorId_when_not_adjacent()
    {
        var s = TwoActors(5, 5, 1, 1);
        var i = new Intention("player", "basic_attack", new Target(ActorId: "bot"));
        Assert.False(Rules.IsLegal(s, i));
    }

    [Fact]
    public void IsLegal_accepts_wait_with_no_target()
    {
        var s = TwoActors(5, 5, 1, 1);
        Assert.True(Rules.IsLegal(s, new Intention("player", "wait")));
    }

    [Fact]
    public void IsLegal_rejects_unknown_action()
    {
        var s = TwoActors(5, 5, 1, 1);
        Assert.False(Rules.IsLegal(s, new Intention("player", "teleport")));
    }
}
