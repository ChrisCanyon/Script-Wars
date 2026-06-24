namespace ArenaGame.Client;

// Screen geometry shared across the client: a square tile grid with an action menu
// strip and an event-log strip stacked beneath it.
static class Layout
{
    public const int TileSize = 48;
    public const int MenuHeight = 96;
    public const int LogHeight = 120;

    public static int WindowWidth(int gridW) => gridW * TileSize;
    public static int WindowHeight(int gridH) => gridH * TileSize + MenuHeight + LogHeight;
}
