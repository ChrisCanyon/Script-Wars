using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace ArenaGame.Client;

// JSON property names from the server are lowercase, so we match them directly.
record StateDto(int width, int height, string[] actions, EntityDto[] entities);
record EntityDto(string id, string kind, int x, int y);
record ActionRequest(string entityId, string type);

static class Program
{
    const int TileSize = 48;
    const string ServerUrl = "http://localhost:5000";
    const string PlayerEntityId = "player";

    // Latest snapshot of server state. Written by the polling task, read by the
    // render loop; guarded by a lock so the render loop always sees a coherent value.
    static readonly object StateLock = new();
    static StateDto? _latestState;

    static readonly HttpClient Http = new() { BaseAddress = new Uri(ServerUrl) };

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    static void Main()
    {
        // Start polling the server in the background before opening the window.
        _ = Task.Run(PollLoop);

        // Size the window from the first state if we already have one; otherwise
        // default to a 12x12 grid and let later states resize logic stay simple.
        StateDto? initial = SnapshotState();
        int gridW = initial?.width ?? 12;
        int gridH = initial?.height ?? 12;

        Raylib.InitWindow(gridW * TileSize, gridH * TileSize, "Arena Game");
        Raylib.SetTargetFPS(60);

        // Textures must be loaded after InitWindow (needs an active GL context).
        Texture2D playerTex = LoadTextureIfPresent("player.png");
        Texture2D[] playerPaletteTex = PlayerPalette.LoadVariants("player-palette.png");
        Texture2D botTex = LoadTextureIfPresent("bot.png");
        Texture2D floorTex = LoadTextureIfPresent("floor.png");

        int windowW = gridW;
        int windowH = gridH;

        while (!Raylib.WindowShouldClose())
        {
            StateDto? state = SnapshotState();

            // Resize the window if the grid dimensions from the server change.
            if (state is not null && (state.width != windowW || state.height != windowH))
            {
                windowW = state.width;
                windowH = state.height;
                Raylib.SetWindowSize(windowW * TileSize, windowH * TileSize);
            }

            HandleInput(state);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            if (state is null)
            {
                Raylib.DrawText("connecting...", 20, 20, 24, Color.RayWhite);
            }
            else
            {
                DrawFloor(state, floorTex);
                DrawEntities(state, playerTex, playerPaletteTex, botTex);
                DrawLegend(state);
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

    static StateDto? SnapshotState()
    {
        lock (StateLock) return _latestState;
    }

    static async Task PollLoop()
    {
        while (true)
        {
            try
            {
                StateDto? state = await Http.GetFromJsonAsync<StateDto>("/state", JsonOpts);
                if (state is not null)
                {
                    lock (StateLock) _latestState = state;
                }
            }
            catch
            {
                // Server may be down or starting up; keep retrying silently.
            }

            await Task.Delay(100);
        }
    }

    static void HandleInput(StateDto? state)
    {
        if (state is null) return;

        string? action = null;
        if (Raylib.IsKeyPressed(KeyboardKey.Up)) action = "MoveUp";
        else if (Raylib.IsKeyPressed(KeyboardKey.Down)) action = "MoveDown";
        else if (Raylib.IsKeyPressed(KeyboardKey.Left)) action = "MoveLeft";
        else if (Raylib.IsKeyPressed(KeyboardKey.Right)) action = "MoveRight";

        if (action is null) return;

        // The server is the authority on legality: only send actions it currently offers.
        if (Array.IndexOf(state.actions, action) < 0) return;

        _ = SendAction(action);
    }

    static async Task SendAction(string type)
    {
        try
        {
            await Http.PostAsJsonAsync("/action", new ActionRequest(PlayerEntityId, type));
        }
        catch
        {
            // Fire-and-forget; ignore transient failures.
        }
    }

    static void DrawFloor(StateDto state, Texture2D floorTex)
    {
        for (int y = 0; y < state.height; y++)
        {
            for (int x = 0; x < state.width; x++)
            {
                int px = x * TileSize;
                int py = y * TileSize;

                if (floorTex.Id != 0)
                    DrawTextureScaled(floorTex, px, py);
                else
                    Raylib.DrawRectangle(px, py, TileSize, TileSize, new Color(30, 30, 35, 255));

                // Light grid lines so individual cells are visible.
                Raylib.DrawRectangleLines(px, py, TileSize, TileSize, new Color(60, 60, 70, 255));
            }
        }
    }

    static void DrawEntities(StateDto state, Texture2D playerTex, Texture2D[] playerPaletteTex, Texture2D botTex)
    {
        foreach (EntityDto e in state.entities)
        {
            // Tile coords -> pixels. Origin is top-left and y increases downward,
            // which matches Raylib's screen space directly, so no flip is needed.
            int px = e.x * TileSize;
            int py = e.y * TileSize;

            bool isPlayer = e.kind == "player";
            Texture2D tex = isPlayer ? PlayerPalette.Pick(playerPaletteTex, e.id) : botTex;
            if (tex.Id == 0 && isPlayer) tex = playerTex;

            if (tex.Id != 0)
            {
                DrawTextureScaled(tex, px, py);
            }
            else
            {
                // Colored-rectangle fallback so the game is playable with no art.
                Color color = isPlayer ? Color.Blue : Color.Red;
                Raylib.DrawRectangle(px, py, TileSize, TileSize, color);
            }
        }
    }

    static void DrawTextureScaled(Texture2D tex, int px, int py)
    {
        Rectangle src = new(0, 0, tex.Width, tex.Height);
        Rectangle dst = new(px, py, TileSize, TileSize);
        Raylib.DrawTexturePro(tex, src, dst, new System.Numerics.Vector2(0, 0), 0f, Color.White);
    }

    static void DrawLegend(StateDto state)
    {
        string legend = "Actions: " + string.Join(' ', state.actions) + "  |  Arrow keys to move";
        int y = state.height * TileSize - 22;
        // Slight shadow for readability over any floor.
        Raylib.DrawText(legend, 7, y + 1, 16, Color.Black);
        Raylib.DrawText(legend, 6, y, 16, Color.RayWhite);
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
