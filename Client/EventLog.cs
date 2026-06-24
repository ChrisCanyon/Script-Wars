using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Scrolling text feed of what happened to the player, drawn in the strip below the menu.
sealed class EventLog
{
    const int MaxLines = 50;
    readonly List<string> _lines = new();

    public void Add(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
    }

    // Append "you hit X" / "X hit you" lines from the just-resolved tick's events.
    public void RecordEvents(ObservationDto obs)
    {
        string me = obs.Self.Id;
        var events = obs.LastTurnResult?.Events;
        if (events is null) return;

        foreach (var e in events)
        {
            if (e.Type != EventTypes.Damage) continue;
            if (e.SourceId == me)
                Add(e.TargetId is null ? "You attack empty space." : $"You hit {Name(e.TargetId)} for {e.Amount}.");
            else if (e.TargetId == me)
                Add($"{Name(e.SourceId)} hit you for {e.Amount}!");
        }
    }

    public void Draw(ObservationDto s)
    {
        int top = s.Height * Layout.TileSize + Layout.MenuHeight;
        int w = s.Width * Layout.TileSize;
        Raylib.DrawRectangle(0, top, w, Layout.LogHeight, new Color(12, 12, 16, 255));
        Raylib.DrawRectangleLines(0, top, w, Layout.LogHeight, new Color(60, 60, 70, 255));
        Raylib.DrawText("LOG", 8, top + 6, 14, new Color(120, 120, 150, 255));

        const int lineH = 16;
        int maxLines = (Layout.LogHeight - 28) / lineH;
        int start = Math.Max(0, _lines.Count - maxLines);
        int y = top + 26;
        for (int i = start; i < _lines.Count; i++)
        {
            Raylib.DrawText(_lines[i], 8, y, 14, Color.RayWhite);
            y += lineH;
        }
    }

    static string Name(string? id) => id switch
    {
        null => "nothing",
        ActorTypes.Player => "you",
        ActorTypes.Bot => "Bot",
        _ => id
    };
}
