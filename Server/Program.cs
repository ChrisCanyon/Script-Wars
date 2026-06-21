using ArenaGame.Game;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// One shared world for this POC.
var world = new World(width: 12, height: 12);

// Server-driven tick loop: advances the world (bot wanders) a few times a second.
// Turn-based later; for now this just makes movement visible.
_ = Task.Run(async () =>
{
    while (true)
    {
        world.Step();
        await Task.Delay(400);
    }
});

// Client polls this to render.
app.MapGet("/state", () => Results.Json(world.Snapshot()));

// Client (human key press, or later a bot) sends an action here.
app.MapPost("/action", (ActionRequest req) =>
{
    world.Apply(req.EntityId, req.Type);
    return Results.Ok();
});

app.Run("http://localhost:5000");

record ActionRequest(string EntityId, string Type);
