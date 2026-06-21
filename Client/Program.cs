using System.Net.Http.Json;
using System.Text.Json;
using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

static class Program
{
    const int TileSize = 48;
    const string ServerUrl = "http://localhost:5000";

    // Latest snapshot of server state. Written by the polling task, read by the
    // render loop; guarded by a lock so the render loop always sees a coherent value.
    static readonly object StateLock = new();
    static ObservationDto? _latestState;

    static readonly HttpClient Http = new() { BaseAddress = new Uri(ServerUrl) };

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    enum Mode { Menu, Targeting, Waiting }
    static Mode _mode = Mode.Menu;
    static string? _selectedActionId;
    const int MenuHeight = 96;

    static void Main()
    {
        // Start polling the server in the background before opening the window.
        _ = Task.Run(PollLoop);

        // Size the window from the first state if we already have one; otherwise
        // default to a 12x12 grid.
        ObservationDto? initial = SnapshotState();
        int gridW = initial?.Width ?? 12;
        int gridH = initial?.Height ?? 12;

        Raylib.InitWindow(gridW * TileSize, gridH * TileSize + MenuHeight, "Arena Game");
        Raylib.SetTargetFPS(60);

        // Textures must be loaded after InitWindow (needs an active GL context).
        Texture2D playerTex = LoadTextureIfPresent("player.png");
        Texture2D[] playerPaletteTex = PlayerPalette.LoadVariants("player-palette.png");
        Texture2D botTex = LoadTextureIfPresent("bot.png");
        Texture2D floorTex = LoadTextureIfPresent("floor.png");

        while (!Raylib.WindowShouldClose())
        {
            ObservationDto? state = SnapshotState();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            if (state is null)
            {
                Raylib.DrawText("connecting...", 20, 20, 24, Color.RayWhite);
            }
            else
            {
                DrawFloor(state, floorTex);
                DrawHighlights(state);
                DrawEntities(state, playerPaletteTex, playerTex, botTex);
                var menuHits = DrawMenu(state);
                HandleMouse(state, menuHits);
            }

            Raylib.EndDrawing();
        }

        // Unload textures before tearing down the GL context.
        PlayerPalette.Unload(playerPaletteTex);
        if (playerTex.Id != 0) Raylib.UnloadTexture(playerTex);
        if (botTex.Id != 0) Raylib.UnloadTexture(botTex);
        if (floorTex.Id != 0) Raylib.UnloadTexture(floorTex);

        Raylib.CloseWindow();
    }

    static ObservationDto? SnapshotState()
    {
        lock (StateLock) return _latestState;
    }

    static async Task PollLoop()
    {
        while (true)
        {
            try
            {
                var obs = await Http.GetFromJsonAsync<ObservationDto>("/state", JsonOpts);
                if (obs is not null) lock (StateLock) _latestState = obs;
            }
            catch { /* server starting up; retry */ }
            await Task.Delay(100);
        }
    }

    static void DrawFloor(ObservationDto s, Texture2D floorTex)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                int px = x * TileSize, py = y * TileSize;
                if (floorTex.Id != 0) DrawTextureScaled(floorTex, px, py);
                else Raylib.DrawRectangle(px, py, TileSize, TileSize, new Color(30, 30, 35, 255));
                Raylib.DrawRectangleLines(px, py, TileSize, TileSize, new Color(60, 60, 70, 255));
            }
    }

    static void DrawEntities(ObservationDto s, Texture2D[] playerPaletteTex, Texture2D playerTex, Texture2D botTex)
    {
        DrawActor(s.Self.Id, s.Self.Type, s.Self.Position.X, s.Self.Position.Y, playerPaletteTex, botTex);
        foreach (var a in s.VisibleActors)
            DrawActor(a.Id, a.Type, a.Position.X, a.Position.Y, playerPaletteTex, botTex);
    }

    static void DrawActor(string id, string type, int x, int y, Texture2D[] playerPaletteTex, Texture2D botTex)
    {
        int px = x * TileSize, py = y * TileSize;
        bool isPlayer = type == "player";
        Texture2D tex = isPlayer ? PlayerPalette.Pick(playerPaletteTex, id) : botTex;
        if (tex.Id != 0)
            DrawTextureScaled(tex, px, py);
        else
            Raylib.DrawRectangle(px, py, TileSize, TileSize, isPlayer ? Color.Blue : Color.Red);
    }

    // Pokémon-style action menu along the bottom. Returns the menu-item rectangles
    // so input handling (Task 9/10) can hit-test clicks.
    static List<(Rectangle rect, ActionDef action)> DrawMenu(ObservationDto s)
    {
        var hits = new List<(Rectangle, ActionDef)>();
        int top = s.Height * TileSize;
        Raylib.DrawRectangle(0, top, s.Width * TileSize, MenuHeight, new Color(20, 20, 28, 255));

        int x = 12, y = top + 12, w = 150, h = 34, gap = 10;
        foreach (var a in s.AvailableActions)
        {
            var rect = new Rectangle(x, y, w, h);
            bool selected = a.Id == _selectedActionId;
            Raylib.DrawRectangleRec(rect, selected ? new Color(80, 80, 120, 255) : new Color(45, 45, 60, 255));
            Raylib.DrawRectangleLinesEx(rect, 1, new Color(120, 120, 150, 255));
            Raylib.DrawText(a.Id, (int)rect.X + 10, (int)rect.Y + 9, 18, Color.RayWhite);
            hits.Add((rect, a));
            x += w + gap;
            if (x + w > s.Width * TileSize) { x = 12; y += h + gap; }
        }

        string hint = _mode switch
        {
            Mode.Targeting => "Select a highlighted tile  (Esc / right-click to cancel)",
            Mode.Waiting   => "Waiting for turn to resolve...",
            _              => "Choose an action"
        };
        Raylib.DrawText(hint, 12, top + MenuHeight - 22, 16, new Color(170, 170, 190, 255));
        return hits;
    }

    // Legal tiles for the currently-selected action, computed locally via Core
    // (the same function the server validates with). Empty when not targeting.
    static List<Position> CurrentTargets(ObservationDto obs)
    {
        if (_mode != Mode.Targeting || _selectedActionId is null) return new();
        var model = StateModel.FromObservation(obs);
        return Rules.LegalTargets(model, obs.Self.Id, _selectedActionId);
    }

    static void DrawHighlights(ObservationDto obs)
    {
        foreach (var p in CurrentTargets(obs))
        {
            int px = p.X * TileSize, py = p.Y * TileSize;
            Raylib.DrawRectangle(px, py, TileSize, TileSize, new Color(80, 200, 120, 90));
            Raylib.DrawRectangleLinesEx(new Rectangle(px, py, TileSize, TileSize), 2,
                new Color(120, 240, 160, 255));
        }
    }

    static void HandleMouse(ObservationDto obs, List<(Rectangle rect, ActionDef action)> menuHits)
    {
        // Cancel: Esc or right-click returns to the menu without spending the turn.
        if (_mode == Mode.Targeting &&
            (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsMouseButtonPressed(MouseButton.Right)))
        {
            _mode = Mode.Menu; _selectedActionId = null;
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();

        // Click on a menu item?
        foreach (var (rect, action) in menuHits)
        {
            if (!Raylib.CheckCollisionPointRec(m, rect)) continue;
            OnMenuClick(action);
            return;
        }

        // Click on the board while targeting?
        if (_mode == Mode.Targeting && _selectedActionId is not null)
        {
            int tx = (int)(m.X / TileSize), ty = (int)(m.Y / TileSize);
            if (CurrentTargets(obs).Contains(new Position(tx, ty)))
                CommitTarget(obs, tx, ty);   // implemented in Task 10
        }
    }

    static void OnMenuClick(ActionDef action)
    {
        if (_mode == Mode.Waiting) return;

        // Re-clicking the selected action (or clicking another) cancels targeting.
        if (_mode == Mode.Targeting && action.Id == _selectedActionId)
        {
            _mode = Mode.Menu; _selectedActionId = null;
            return;
        }

        _selectedActionId = action.Id;
        if (action.TargetType == "none" || action.TargetType == "self")
            CommitImmediate(action);         // implemented in Task 10
        else
            _mode = Mode.Targeting;
    }

    static void CommitImmediate(ActionDef action) { _mode = Mode.Menu; _selectedActionId = null; }
    static void CommitTarget(ObservationDto obs, int tx, int ty) { _mode = Mode.Menu; _selectedActionId = null; }

    static void DrawTextureScaled(Texture2D tex, int px, int py)
    {
        Rectangle src = new(0, 0, tex.Width, tex.Height);
        Rectangle dst = new(px, py, TileSize, TileSize);
        Raylib.DrawTexturePro(tex, src, dst, new System.Numerics.Vector2(0, 0), 0f, Color.White);
    }

    static Texture2D LoadTextureIfPresent(string fileName)
    {
        // Assets are copied to "<output>/assets/" at build time. Resolve relative to the
        // executable (not the cwd, which differs under `dotnet run`). A missing file is
        // treated as "use the colored-rectangle fallback".
        string path = Path.Combine(AppContext.BaseDirectory, "assets", fileName);
        if (!File.Exists(path)) return default;
        return Raylib.LoadTexture(path);
    }
}
