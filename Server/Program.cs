using ArenaGame.Core;
using ArenaGame.Game;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// One shared world for this POC.
var world = new World(width: 12, height: 12);

// Turn-based now: no background tick loop. The world resolves a tick when every
// actor has a pending intention (the bot auto-readies; the human POSTs /action).

// Client polls this to render (observation for the player's perspective).
app.MapGet("/state", () => Results.Json(world.Snapshot()));

// Human submits an intention. Server is the authority: illegal -> 400.
app.MapPost("/action", (Intention intention) =>
    world.Submit(intention) ? Results.Ok() : Results.BadRequest(new { error = "illegal action" }));

app.Run("http://localhost:5000");
