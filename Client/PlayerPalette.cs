using Raylib_cs;

namespace ArenaGame.Client;

static class PlayerPalette
{
    // Sentinel colors expected in the source PNG. Alpha is ignored for matching,
    // so the same markers can be used on antialiased pixels if needed.
    static readonly Color ShadowSentinel = new(0, 0, 1, 255);
    static readonly Color MidSentinel = new(0, 0, 2, 255);
    static readonly Color HighlightSentinel = new(0, 0, 3, 255);

    // Each palette is [shadow, mid, highlight], mapped onto the (0,0,1)/(0,0,2)/(0,0,3)
    // sentinel pixels in the source sprite. 0=red 1=green 2=blue 3=purple 4=gold.
    static readonly Color[][] Palettes =
    [
        [new Color(86, 14, 26, 255), new Color(166, 38, 55, 255), new Color(238, 89, 92, 255)],
        [new Color(18, 88, 44, 255), new Color(37, 164, 77, 255), new Color(89, 226, 124, 255)],
        [new Color(28, 70, 138, 255), new Color(38, 118, 214, 255), new Color(91, 177, 255, 255)],
        [new Color(73, 34, 124, 255), new Color(125, 61, 210, 255), new Color(190, 116, 255, 255)],
        [new Color(112, 77, 14, 255), new Color(191, 137, 28, 255), new Color(255, 205, 76, 255)],
    ];

    // The player's color. Change this index to recolor the player (see palette list above).
    public const int PlayerColorIndex = 2;

    // Load one sprite and recolor its sentinel pixels to the given palette.
    // Returns default (Id 0) if the file is missing — caller falls back to a flat color.
    public static Texture2D LoadTinted(string assetPath, int paletteIndex)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", assetPath);
        if (!File.Exists(path)) return default;

        Image img = Raylib.LoadImage(path);
        try
        {
            ApplyPalette(ref img, Palettes[paletteIndex]);
            return Raylib.LoadTextureFromImage(img);
        }
        finally { Raylib.UnloadImage(img); }
    }

    public static Texture2D[] LoadVariants(string assetPath)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "assets", assetPath);
        if (!File.Exists(path)) return [];

        Image source = Raylib.LoadImage(path);
        try
        {
            Texture2D[] variants = new Texture2D[Palettes.Length];

            for (int i = 0; i < Palettes.Length; i++)
            {
                Image recolored = Raylib.ImageCopy(source);
                try
                {
                    ApplyPalette(ref recolored, Palettes[i]);
                    variants[i] = Raylib.LoadTextureFromImage(recolored);
                }
                finally
                {
                    Raylib.UnloadImage(recolored);
                }
            }

            return variants;
        }
        finally
        {
            Raylib.UnloadImage(source);
        }
    }

    public static Texture2D Pick(Texture2D[] variants, string entityId)
    {
        if (variants.Length == 0) return default;

        int hash = 0;
        foreach (char c in entityId)
        {
            hash = unchecked((hash * 31) + c);
        }

        int index = Math.Abs(hash % variants.Length);
        return variants[index];
    }

    public static void Unload(Texture2D[] variants)
    {
        foreach (Texture2D texture in variants)
        {
            if (texture.Id != 0) Raylib.UnloadTexture(texture);
        }
    }

    static void ApplyPalette(ref Image image, Color[] palette)
    {
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Color pixel = Raylib.GetImageColor(image, x, y);
                Color replacement;

                if (SameRgb(pixel, ShadowSentinel))
                    replacement = palette[0];
                else if (SameRgb(pixel, MidSentinel))
                    replacement = palette[1];
                else if (SameRgb(pixel, HighlightSentinel))
                    replacement = palette[2];
                else
                    continue;

                replacement.A = pixel.A;
                Raylib.ImageDrawPixel(ref image, x, y, replacement);
            }
        }
    }

    static bool SameRgb(Color left, Color right)
    {
        return left.R == right.R && left.G == right.G && left.B == right.B;
    }
}
