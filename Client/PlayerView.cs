using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Per-actor view state derived from observations — which way each actor faces (from the
// direction it last moved) and the idle bob — plus the player's transient attack swing.
// Picks the sprite to draw for a player actor.
sealed class PlayerView
{
    const float IdleBobAmplitude = 2.5f;   // ~5px peak-to-peak
    const float IdleBobSpeed = 3f;
    const int AttackFrames = 4;
    const double AttackAnimDuration = 0.4;

    readonly Dictionary<string, (int x, int y)> _lastPos = new();
    readonly Dictionary<string, string> _facing = new();
    double? _attackStart;
    string _attackDir = "down";

    // Update each actor's facing from how it moved this tick. An actor that didn't move
    // keeps its facing; an actor we haven't seen defaults to "down".
    public void Observe(ObservationDto s)
    {
        Track(s.Self.Id, s.Self.Position.X, s.Self.Position.Y);
        foreach (var a in s.VisibleActors)
            Track(a.Id, a.Position.X, a.Position.Y);
    }

    void Track(string id, int x, int y)
    {
        if (_lastPos.TryGetValue(id, out var prev))
        {
            int dx = x - prev.x, dy = y - prev.y;
            if (dx < 0) _facing[id] = "left";
            else if (dx > 0) _facing[id] = "right";
            else if (dy < 0) _facing[id] = "up";
            else if (dy > 0) _facing[id] = "down";
            // dx == dy == 0: didn't move, keep the existing facing.
        }
        _lastPos[id] = (x, y);
    }

    // Begin the attack swing, turning the player to face the target tile.
    public void StartAttack(string playerId, Position from, Position to)
    {
        _attackDir = DirTo(from, to);
        _facing[playerId] = _attackDir;
        _attackStart = Raylib.GetTime();
    }

    // Cosmetic vertical bob, with a per-actor phase so actors don't bounce in lockstep.
    public int BobOffset(string id)
    {
        double phase = (uint)id.GetHashCode() % 360 * Math.PI / 180.0;
        return (int)Math.Round(Math.Sin(Raylib.GetTime() * IdleBobSpeed + phase) * IdleBobAmplitude);
    }

    // Sprite for a player actor: the current attack frame while mid-swing, otherwise the
    // idle sprite for its facing. Returns default (Id 0) if no texture is available.
    public Texture2D PlayerSprite(string id, AssetBundle assets)
    {
        string facing = _facing.GetValueOrDefault(id, "down");
        Texture2D tex = assets.PlayerIdle.GetValueOrDefault(facing);
        if (tex.Id == 0) tex = assets.PlayerIdle.GetValueOrDefault("down");

        if (_attackStart is double st)
        {
            double age = Raylib.GetTime() - st;
            if (age < AttackAnimDuration && assets.PlayerAttack.TryGetValue(_attackDir, out var frames))
            {
                int frame = Math.Clamp((int)(age / AttackAnimDuration * AttackFrames), 0, AttackFrames - 1);
                if (frames[frame].Id != 0) tex = frames[frame];
            }
        }
        return tex;
    }

    // Cardinal direction from one tile toward another (ties favor horizontal).
    public static string DirTo(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy)) return dx < 0 ? "left" : "right";
        return dy < 0 ? "up" : "down";
    }
}
