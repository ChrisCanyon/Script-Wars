using Raylib_cs;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Stateless drawing of the board, terrain, actors, health bars, target highlights, and
// the action menu. Given the current observation plus the view/assets, it just draws.
static class Renderer
{
    public static void DrawConnecting() =>
        Raylib.DrawText("connecting...", 20, 20, 24, Color.RayWhite);

    public static void DrawFloor(ObservationDto s, AssetBundle assets)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                int px = x * Layout.TileSize, py = y * Layout.TileSize;
                if (assets.Floor.Id != 0) DrawScaled(assets.Floor, px, py);
                else Raylib.DrawRectangle(px, py, Layout.TileSize, Layout.TileSize, new Color(30, 30, 35, 255));
                Raylib.DrawRectangleLines(px, py, Layout.TileSize, Layout.TileSize, new Color(60, 60, 70, 255));
            }
    }

    // Blocked tiles over the floor: wall.png for the border, boulder.png inside.
    public static void DrawTerrain(ObservationDto s, AssetBundle assets)
    {
        foreach (var t in s.VisibleTiles)
        {
            if (!t.Blocked) continue;
            int px = t.X * Layout.TileSize, py = t.Y * Layout.TileSize;
            Texture2D tex = t.Terrain == Terrains.Boulder ? assets.Boulder : assets.Wall;
            if (tex.Id != 0)
                DrawScaled(tex, px, py);
            else
                Raylib.DrawRectangle(px, py, Layout.TileSize, Layout.TileSize,
                    t.Terrain == Terrains.Boulder ? new Color(90, 80, 70, 255) : new Color(70, 70, 80, 255));
        }
    }

    public static void DrawHighlights(ObservationDto obs, IReadOnlyList<Position> targets)
    {
        foreach (var p in targets)
        {
            int px = p.X * Layout.TileSize, py = p.Y * Layout.TileSize;
            Raylib.DrawRectangle(px, py, Layout.TileSize, Layout.TileSize, new Color(80, 200, 120, 90));
            Raylib.DrawRectangleLinesEx(new Rectangle(px, py, Layout.TileSize, Layout.TileSize), 2,
                new Color(120, 240, 160, 255));
        }
    }

    public static void DrawEntities(ObservationDto s, AssetBundle assets, PlayerView view)
    {
        DrawActor(s.Self.Id, s.Self.Type, s.Self.Position.X, s.Self.Position.Y, s.Self.Hp, s.Self.MaxHp, assets, view);
        foreach (var a in s.VisibleActors)
            DrawActor(a.Id, a.Type, a.Position.X, a.Position.Y, a.Hp, a.MaxHp, assets, view);
    }

    static void DrawActor(string id, string type, int x, int y, int hp, int maxHp, AssetBundle assets, PlayerView view)
    {
        int px = x * Layout.TileSize;
        int py = y * Layout.TileSize + view.BobOffset(id);
        bool isPlayer = type == ActorTypes.Player;

        Texture2D tex = isPlayer ? view.PlayerSprite(id, assets) : assets.Bot;
        if (tex.Id != 0)
            DrawScaled(tex, px, py);
        else
            Raylib.DrawRectangle(px, py, Layout.TileSize, Layout.TileSize, isPlayer ? Color.Blue : Color.Red);

        DrawHealthBar(x, y, hp, maxHp);
    }

    // A small hp bar pinned just above the actor's tile (not bobbing, so it stays steady).
    static void DrawHealthBar(int tileX, int tileY, int hp, int maxHp)
    {
        if (maxHp <= 0) return;
        int barW = Layout.TileSize - 8, barH = 5;
        int bx = tileX * Layout.TileSize + 4, by = tileY * Layout.TileSize - barH - 2;
        float frac = Math.Clamp(hp / (float)maxHp, 0f, 1f);
        Raylib.DrawRectangle(bx, by, barW, barH, new Color(40, 0, 0, 220));   // empty (dark red)
        Raylib.DrawRectangle(bx, by, (int)(barW * frac), barH,                 // filled (green→red)
            frac > 0.5f ? new Color(70, 200, 90, 255)
            : frac > 0.25f ? new Color(220, 190, 60, 255)
            : new Color(220, 70, 60, 255));
        Raylib.DrawRectangleLines(bx, by, barW, barH, new Color(10, 10, 12, 255));
    }

    // Pokémon-style action menu along the bottom. Returns the menu-item rectangles so the
    // turn controller can hit-test clicks.
    public static List<(Rectangle rect, ActionDef action)> DrawMenu(ObservationDto s, string? selectedActionId, string hint)
    {
        var hits = new List<(Rectangle, ActionDef)>();
        int top = s.Height * Layout.TileSize;
        Raylib.DrawRectangle(0, top, s.Width * Layout.TileSize, Layout.MenuHeight, new Color(20, 20, 28, 255));

        int x = 12, y = top + 12, w = 150, h = 34, gap = 10;
        foreach (var a in s.AvailableActions)
        {
            var rect = new Rectangle(x, y, w, h);
            bool selected = a.Id == selectedActionId;
            Raylib.DrawRectangleRec(rect, selected ? new Color(80, 80, 120, 255) : new Color(45, 45, 60, 255));
            Raylib.DrawRectangleLinesEx(rect, 1, new Color(120, 120, 150, 255));
            Raylib.DrawText(a.Id, (int)rect.X + 10, (int)rect.Y + 9, 18, Color.RayWhite);
            hits.Add((rect, a));
            x += w + gap;
            if (x + w > s.Width * Layout.TileSize) { x = 12; y += h + gap; }
        }

        Raylib.DrawText(hint, 12, top + Layout.MenuHeight - 22, 16, new Color(170, 170, 190, 255));
        return hits;
    }

    static void DrawScaled(Texture2D tex, int px, int py)
    {
        Rectangle src = new(0, 0, tex.Width, tex.Height);
        Rectangle dst = new(px, py, Layout.TileSize, Layout.TileSize);
        Raylib.DrawTexturePro(tex, src, dst, new System.Numerics.Vector2(0, 0), 0f, Color.White);
    }
}
