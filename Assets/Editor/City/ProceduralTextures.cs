using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Generates tileable facade and ground textures and saves them as PNG assets.</summary>
static class ProceduralTextures
{
    public const string Folder = "Assets/Generated/Textures";
    const int Size = 1024;
    const int Bays = 8; // window bays per texture tile, horizontally and vertically

    static readonly Color LitColor = new Color(1f, 0.8f, 0.55f);
    // Interior light colours for lit windows at night: warm homes, neutral and cool offices.
    static readonly Color[] NightColors =
    {
        new Color(1f, 0.72f, 0.42f), new Color(1f, 0.8f, 0.55f), new Color(1f, 0.92f, 0.78f), new Color(0.78f, 0.88f, 1f),
    };

    public class FacadeStyle
    {
        public string Name;
        public Vector2 BayMeters = new Vector2(3f, 4f);
        public Color Wall = Color.gray;
        public Color Glass = new Color(0.15f, 0.2f, 0.25f);
        public Color Frame = new Color(0.75f, 0.77f, 0.8f);
        public Color Spandrel = Color.clear; // band above/below windows; clear = same as wall
        public float WindowWidth = 0.8f, WindowHeight = 0.6f, WindowBottom = 0.2f;
        public float Mullion; // centre mullion width as a fraction of the bay, 0 = none
        public float WallSmoothness = 0.3f, GlassSmoothness = 0.95f, GlassMetallic = 0.4f;
        public bool Brick;
        public float LitChance = 0.1f;   // windows with lights visibly on in daytime (albedo tint only)
        public float NightLit = 0.3f;    // windows lit at night (emission map)
        public int Seed = 1;
    }

    public class FacadeMaps
    {
        public Texture2D Albedo, Mask, Emission;
        public Vector2 TileMeters;
    }

    public static FacadeMaps Facade(FacadeStyle s)
    {
        var albedo = new Color32[Size * Size];
        var mask = new Color32[Size * Size];
        var emission = new Color32[Size * Size];

        var random = new System.Random(s.Seed);
        var lit = new bool[Bays, Bays];
        var tint = new float[Bays, Bays];
        var nightLight = new Color[Bays, Bays];
        for (int by = 0; by < Bays; by++)
        for (int bx = 0; bx < Bays; bx++)
        {
            float roll = (float)random.NextDouble();
            lit[bx, by] = roll < s.LitChance;
            tint[bx, by] = (float)random.NextDouble();
            float brightness = 0.45f + 0.55f * (float)random.NextDouble();
            nightLight[bx, by] = roll < s.NightLit
                ? NightColors[random.Next(NightColors.Length)] * brightness
                : Color.black;
        }

        int bay = Size / Bays;
        float frame = 3f / bay;
        bool hasSpandrel = s.Spandrel.a > 0f;
        float windowTop = s.WindowBottom + s.WindowHeight;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int bx = x / bay, by = y / bay;
            float u = (x % bay + 0.5f) / bay, v = (y % bay + 0.5f) / bay;

            // Signed distance (in bay units) to the window rectangle: negative inside.
            float d = Mathf.Max(Mathf.Abs(u - 0.5f) - s.WindowWidth / 2f, Mathf.Max(s.WindowBottom - v, v - windowTop));
            bool mullion = s.Mullion > 0f && Mathf.Abs(u - 0.5f) < s.Mullion / 2f;
            float grain = 0.93f + 0.07f * Fbm(x / (float)Size, y / (float)Size, 16, 3, s.Seed);

            Color c, e = Color.black;
            float metal, smooth;
            if (d < 0f && !mullion)
            {
                float wv = Mathf.InverseLerp(s.WindowBottom, windowTop, v);
                c = s.Glass * (0.75f + 0.45f * wv + 0.2f * (tint[bx, by] - 0.5f));
                metal = s.GlassMetallic;
                smooth = s.GlassSmoothness;
                if (lit[bx, by])
                    c = Color.Lerp(c, LitColor * 0.6f, 0.3f);
                // Slightly brighter towards the ceiling, like a lit room seen from outside.
                e = nightLight[bx, by] * (0.75f + 0.25f * wv);
            }
            else if (d < frame)
            {
                c = s.Frame * grain;
                metal = 0.6f;
                smooth = 0.55f;
            }
            else
            {
                bool inBand = v >= s.WindowBottom && v <= windowTop;
                bool spandrel = hasSpandrel && !inBand;
                c = spandrel ? s.Spandrel : s.Wall;
                if (s.Brick)
                {
                    int row = y / 6;
                    if (y % 6 == 0 || (x + (row % 2) * 6) % 12 == 0) c *= 0.78f;
                }
                c *= grain;
                metal = spandrel ? 0.3f : 0f;
                smooth = spandrel ? s.GlassSmoothness * 0.85f : s.WallSmoothness;
            }

            int i = y * Size + x;
            c.a = 1f;
            albedo[i] = c;
            mask[i] = new Color(metal, 0f, 0f, smooth);
            emission[i] = e;
        }

        return new FacadeMaps
        {
            Albedo = Save(s.Name + " Albedo", albedo, true),
            Mask = Save(s.Name + " Mask", mask, false),
            Emission = Save(s.Name + " Emission", emission, true),
            TileMeters = s.BayMeters * Bays,
        };
    }

    /// <summary>Square paving slabs with grout lines; <paramref name="tiles"/> slabs per texture side.</summary>
    public static Texture2D Paving(string name, Color color, int tiles, int seed)
    {
        var pixels = new Color32[Size * Size];
        int tile = Size / tiles;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Color c;
            if (x % tile < 3 || y % tile < 3)
                c = color * 0.62f;
            else
                c = color * (0.9f + 0.14f * Hash(x / tile, y / tile, seed))
                          * (0.94f + 0.1f * Fbm(x / (float)Size, y / (float)Size, 32, 3, seed));
            c.a = 1f;
            pixels[y * Size + x] = c;
        }
        return Save(name, pixels, true);
    }

    /// <summary>Wooden floorboards: <paramref name="rows"/> planks per texture, staggered joints, streaky grain.</summary>
    public static Texture2D Planks(string name, Color color, int rows, int seed)
    {
        var pixels = new Color32[Size * Size];
        int rowHeight = Size / rows;
        var random = new System.Random(seed);
        var joints = new int[rows];
        var tones = new float[rows, 4];
        for (int r = 0; r < rows; r++)
        {
            joints[r] = random.Next(Size);
            for (int k = 0; k < 4; k++) tones[r, k] = 0.82f + 0.3f * (float)random.NextDouble();
        }

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            // y * rows / Size (not y / rowHeight) stays in range when rows doesn't divide the texture size.
            int row = y * rows / Size;
            int rowStart = row * Size / rows;
            // Two planks per row, joints staggered from row to row.
            int along = (x + joints[row]) % Size;
            int plank = along / (Size / 2);
            bool seam = y - rowStart < 2 || along % (Size / 2) < 2;
            float grain = Fbm(x / (float)Size, y / (float)Size * 0.12f + row * 0.37f, 48, 3, seed + row);
            var c = color * tones[row, plank + (row % 2) * 2] * (0.85f + 0.3f * grain);
            if (seam) c *= 0.55f;
            c.a = 1f;
            pixels[y * Size + x] = c;
        }
        return Save(name, pixels, true);
    }

    /// <summary>Polished marble: soft base with thin, wandering veins (tileable).</summary>
    public static Texture2D Marble(string name, Color baseColor, Color vein, int seed)
    {
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float u = x / (float)Size, v = y / (float)Size;
            float warp = Fbm(u, v, 2, 4, seed);
            // Integer frequencies keep the veins tileable; a high power keeps them thin and sparse.
            float band = Mathf.Abs(Mathf.Sin((1f * u + 1f * v + 1.6f * warp) * Mathf.PI * 2f));
            float veins = Mathf.Pow(1f - band, 60f) + 0.35f * Mathf.Pow(1f - Mathf.Abs(Mathf.Sin((2f * v - u + 2.5f * warp) * Mathf.PI * 2f)), 90f);
            float cloud = Fbm(u, v, 6, 4, seed + 5);
            var c = Color.Lerp(baseColor * (0.93f + 0.08f * cloud), vein, Mathf.Clamp01(veins) * 0.7f);
            c.a = 1f;
            pixels[y * Size + x] = c;
        }
        return Save(name, pixels, true);
    }

    /// <summary>Interior brick wall: staggered bricks with varied tone and recessed mortar.</summary>
    public static Texture2D Bricks(string name, Color brick, Color mortar, int seed)
    {
        const int rows = 16, perRow = 8;
        var pixels = new Color32[Size * Size];
        int rowHeight = Size / rows, brickWidth = Size / perRow;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int row = y / rowHeight;
            int shifted = x + (row % 2) * brickWidth / 2;
            int column = (shifted / brickWidth) % perRow;
            bool joint = y % rowHeight < 4 || shifted % brickWidth < 4;
            Color c = joint
                ? mortar * (0.9f + 0.1f * Hash(x, y, seed))
                : brick * (0.78f + 0.3f * Hash(column, row, seed)) * (0.9f + 0.15f * Fbm(x / (float)Size, y / (float)Size, 32, 3, seed));
            c.a = 1f;
            pixels[y * Size + x] = c;
        }
        return Save(name, pixels, true);
    }

    /// <summary>Two-colour noise (asphalt, grass) with fine speckle.</summary>
    public static Texture2D Noise(string name, Color a, Color b, int cells, int seed)
    {
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float u = x / (float)Size, v = y / (float)Size;
            float t = Fbm(u, v, cells, 4, seed);
            float speckle = Hash(x, y, seed + 99) * 0.12f - 0.06f;
            var c = Color.Lerp(a, b, t) * (1f + speckle);
            c.a = 1f;
            pixels[y * Size + x] = c;
        }
        return Save(name, pixels, true);
    }

    // ---------------------------------------------------------------- Noise

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    static int Wrap(int i, int period) => ((i % period) + period) % period;

    static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int ax = Wrap(x0, period), bx = Wrap(x0 + 1, period);
        int ay = Wrap(y0, period), by = Wrap(y0 + 1, period);
        return Mathf.Lerp(
            Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx),
            Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx), fy);
    }

    /// <summary>Tileable fractal noise in [0, 1] for UVs in [0, 1).</summary>
    static float Fbm(float u, float v, int cells, int octaves, int seed)
    {
        float sum = 0f, amplitude = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++, cells *= 2, amplitude *= 0.5f)
        {
            sum += amplitude * ValueNoise(u * cells, v * cells, cells, seed + o);
            norm += amplitude;
        }
        return sum / norm;
    }

    // ---------------------------------------------------------------- Saving

    static Texture2D Save(string name, Color32[] pixels, bool sRGB)
    {
        string path = $"{Folder}/{name}.png";
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = sRGB;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
