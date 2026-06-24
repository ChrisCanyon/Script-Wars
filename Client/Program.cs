using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Composition root: wires the pieces together and runs the draw/input loop. Each concern
// lives in its own class — networking (ServerConnection), textures (AssetBundle), per-actor
// view state (PlayerView), drawing (Renderer), the turn state machine (TurnController), and
// the feedback widgets (EventLog, HitSplats).
static class Program
{
    static void Main()
    {
        var server = new ServerConnection();
        server.StartPolling();

        // Size the window from the first observation if we have one yet; else default 12x12.
        ObservationDto? initial = server.Latest();
        int gridW = initial?.Width ?? 12;
        int gridH = initial?.Height ?? 12;

        Raylib.InitWindow(Layout.WindowWidth(gridW), Layout.WindowHeight(gridH), "Arena Game");
        Raylib.SetTargetFPS(60);

        var assets = new AssetBundle();   // after InitWindow: textures need an active GL context
        var view = new PlayerView();
        var log = new EventLog();
        var splats = new HitSplats();
        var turn = new TurnController(server, view, log, splats);

        while (!Raylib.WindowShouldClose())
        {
            ObservationDto? state = server.Latest();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            if (state is null)
            {
                Renderer.DrawConnecting();
            }
            else
            {
                view.Observe(state);
                Renderer.DrawFloor(state, assets);
                Renderer.DrawTerrain(state, assets);
                Renderer.DrawHighlights(state, turn.CurrentTargets(state));
                Renderer.DrawEntities(state, assets, view);
                splats.Draw(assets);
                var menuHits = Renderer.DrawMenu(state, turn.SelectedActionId, turn.ModeHint);
                log.Draw(state);
                turn.HandleMouse(state, menuHits);
            }

            Raylib.EndDrawing();
        }

        assets.Unload();   // free GPU textures before tearing down the GL context
        Raylib.CloseWindow();
    }
}
