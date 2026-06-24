using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Short-lived hit splats popped on tiles that took damage, growing and fading over a
// fraction of a second.
sealed class HitSplats
{
    const double SplatDuration = 0.45;
    readonly List<(int x, int y, double t)> _splats = new();

    // Pop a splat on every tile that took damage in the just-resolved tick.
    public void RegisterFromEvents(ObservationDto obs)
    {
        var events = obs.LastTurnResult?.Events;
        if (events is null) return;

        // Where each actor ended up this tick, to place a splat on whoever was hit.
        var pos = new Dictionary<string, Position> { [obs.Self.Id] = obs.Self.Position };
        foreach (var a in obs.VisibleActors) pos[a.Id] = a.Position;

        foreach (var e in events)
            if (e.Type == EventTypes.Damage && e.TargetId is not null && pos.TryGetValue(e.TargetId, out var p))
                _splats.Add((p.X, p.Y, Raylib.GetTime()));
    }

    public void Draw(AssetBundle assets)
    {
        double now = Raylib.GetTime();
        _splats.RemoveAll(sp => now - sp.t > SplatDuration);
        if (assets.HitSplat.Id == 0) return;

        foreach (var sp in _splats)
        {
            float r = (float)((now - sp.t) / SplatDuration);   // 0 → 1 over its life
            int cx = sp.x * Layout.TileSize + Layout.TileSize / 2;
            int cy = sp.y * Layout.TileSize + Layout.TileSize / 2;
            float size = 30 + 18 * r;                           // small pop as it fades
            byte alpha = (byte)(255 * (1 - r));
            Rectangle src = new(0, 0, assets.HitSplat.Width, assets.HitSplat.Height);
            Rectangle dst = new(cx - size / 2, cy - size / 2, size, size);
            Raylib.DrawTexturePro(assets.HitSplat, src, dst, new System.Numerics.Vector2(0, 0), 0f,
                new Color(255, 255, 255, (int)alpha));
        }
    }
}
