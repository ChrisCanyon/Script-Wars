using Raylib_cs;

namespace ArenaGame.Client;

// Loads and owns every texture the client draws. Construct AFTER InitWindow (textures
// need an active GL context); call Unload() before CloseWindow. A missing file yields a
// default texture (Id 0), which callers treat as "draw the colored-rectangle fallback".
sealed class AssetBundle
{
    static readonly string[] Directions = { "up", "down", "left", "right" };
    const int AttackFrames = 4;

    // Player sprites are recolored from their sentinel pixels to the team color.
    public Dictionary<string, Texture2D> PlayerIdle { get; } = new();
    public Dictionary<string, Texture2D[]> PlayerAttack { get; } = new();
    public Texture2D Bot { get; }
    public Texture2D Floor { get; }
    public Texture2D HitSplat { get; }
    public Texture2D Wall { get; }
    public Texture2D Boulder { get; }

    public AssetBundle()
    {
        foreach (string dir in Directions)
        {
            PlayerIdle[dir] = PlayerPalette.LoadTinted(Path.Combine("player", dir, "idle-0.png"), PlayerPalette.PlayerColorIndex);
            var frames = new Texture2D[AttackFrames];
            for (int i = 0; i < AttackFrames; i++)
                frames[i] = PlayerPalette.LoadTinted(Path.Combine("player", dir, $"attack-{i}.png"), PlayerPalette.PlayerColorIndex);
            PlayerAttack[dir] = frames;
        }

        Bot = LoadIfPresent("bot.png");
        Floor = LoadIfPresent("floor.png");
        HitSplat = LoadIfPresent(Path.Combine("effects", "hitsplats", "physical.png"));
        Wall = LoadIfPresent("wall.png");
        Boulder = LoadIfPresent(Path.Combine("effects", "ground", "boulder.png"));
    }

    public void Unload()
    {
        foreach (Texture2D t in PlayerIdle.Values) TryUnload(t);
        foreach (Texture2D[] frames in PlayerAttack.Values)
            foreach (Texture2D t in frames) TryUnload(t);
        TryUnload(Bot);
        TryUnload(Floor);
        TryUnload(HitSplat);
        TryUnload(Wall);
        TryUnload(Boulder);
    }

    static void TryUnload(Texture2D t)
    {
        if (t.Id != 0) Raylib.UnloadTexture(t);
    }

    static Texture2D LoadIfPresent(string fileName)
    {
        // Assets are copied to "<output>/assets/" at build time; resolve relative to the
        // executable, not the cwd (which differs under `dotnet run`).
        string path = Path.Combine(AppContext.BaseDirectory, "assets", fileName);
        if (!File.Exists(path)) return default;
        return Raylib.LoadTexture(path);
    }
}
