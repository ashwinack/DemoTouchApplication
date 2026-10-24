using UnityEngine;

/// <summary>Camera presets and lights generated for one apartment.</summary>
public class ApartmentInfo
{
    public string Label, Details;
    public Transform Root, Entry, Interior;
    public GameObject Lights;
    public Vector3 Size; // width, height, depth
}

/// <summary>Interior design theme; each hero building uses a different one.</summary>
public enum InteriorTheme
{
    Luxury,     // Azure Tower: marble, brass, velvet, black kitchen, chandelier
    Natural,    // Terrace Center: oak, terracotta, linen, beams, lots of plants
    Industrial, // Helix Tower: concrete, brick, steel, glass partitions, exposed ducts
}

/// <summary>
/// Builds furnished apartment interiors (1 BHK and 2 BHK) inside the hero buildings, in one of three themes
/// that change the finishes, furniture arrangement, lighting and architectural details.
/// Local space: origin at floor level in the middle of the window wall, +z runs into the apartment,
/// the window wall faces -z (out of the building). Furniture "fronts" face local -z.
/// The building shell is a closed box whose faces point outwards, so from inside it is culled and
/// the windows look straight out at the city.
/// </summary>
static class ApartmentBuilder
{
    const float WallThickness = 0.12f, DoorWidth = 1f, DoorHeight = 2.1f;

    class Style
    {
        public InteriorTheme Theme;
        public string Tagline;
        public int OneBhkSqFt, TwoBhkSqFt;
        public float Height;
        public Material Floor, Wall, Accent, Ceiling, Sofa, Cushion, Linen, Duvet, Headboard, Wood, Cabinet, Counter,
            Metal, Rug, Leaves, Pot, Screen, Lamp, Glass, Tile, Frame, Partition, Strip;
        public Material[] Art;
        public Color LightColor;
        public float LightIntensity;
    }

    static Style[] styles;
    static Style s;       // the style of the apartment being built
    static float Height;  // its ceiling height

    public static void CreateMaterials()
    {
        styles = new Style[3];
        var glass = CityBuilder.Transparent("Window Glass", new Color(0.75f, 0.85f, 0.9f, 0.1f), 0.3f);
        var screen = CityBuilder.Solid("TV Screen", new Color(0.02f, 0.02f, 0.03f), 0.95f);
        var white = CityBuilder.Solid("Linen White", new Color(0.93f, 0.92f, 0.9f), 0.1f);

        styles[(int)InteriorTheme.Luxury] = new Style
        {
            Theme = InteriorTheme.Luxury,
            Tagline = "Luxury finish · marble, brass & velvet",
            OneBhkSqFt = 720, TwoBhkSqFt = 1180, Height = 3.2f,
            Floor = CityBuilder.Textured("Lux Marble Floor", ProceduralTextures.Marble("Lux Marble", new Color(0.93f, 0.92f, 0.9f), new Color(0.5f, 0.5f, 0.54f), 41), 2.4f, 0.85f),
            Counter = CityBuilder.Textured("Lux Marble Counter", ProceduralTextures.Marble("Lux Marble Dark", new Color(0.12f, 0.12f, 0.13f), new Color(0.75f, 0.72f, 0.66f), 42), 1.2f, 0.9f),
            Wall = CityBuilder.Solid("Lux Wall", new Color(0.95f, 0.94f, 0.92f), 0.2f),
            Accent = CityBuilder.Solid("Lux Navy Wall", new Color(0.07f, 0.11f, 0.2f), 0.35f),
            Ceiling = CityBuilder.Solid("Lux Ceiling", new Color(0.97f, 0.97f, 0.96f), 0.1f),
            Sofa = CityBuilder.Solid("Lux Emerald Velvet", new Color(0.05f, 0.28f, 0.22f), 0.4f),
            Cushion = CityBuilder.Solid("Lux Gold Cushion", new Color(0.85f, 0.64f, 0.3f), 0.45f),
            Linen = white,
            Duvet = CityBuilder.Solid("Lux Champagne Duvet", new Color(0.82f, 0.75f, 0.62f), 0.3f),
            Headboard = CityBuilder.Solid("Lux Navy Velvet", new Color(0.1f, 0.14f, 0.26f), 0.4f),
            Wood = CityBuilder.Solid("Lux Walnut", new Color(0.24f, 0.14f, 0.08f), 0.65f),
            Cabinet = CityBuilder.Solid("Lux Black Lacquer", new Color(0.04f, 0.04f, 0.05f), 0.88f),
            Metal = CityBuilder.Solid("Lux Brass", new Color(0.86f, 0.66f, 0.32f), 0.78f, 1f),
            Rug = CityBuilder.Textured("Lux Rug", ProceduralTextures.Noise("Lux Rug", new Color(0.48f, 0.48f, 0.5f), new Color(0.62f, 0.62f, 0.64f), 20, 43), 1f, 0.05f),
            Leaves = CityBuilder.Solid("Lux Leaves", new Color(0.12f, 0.3f, 0.14f), 0.3f),
            Pot = CityBuilder.Solid("Lux Black Ceramic", new Color(0.05f, 0.05f, 0.06f), 0.85f),
            Screen = screen,
            Lamp = CityBuilder.Solid("Lux Lamp", new Color(1f, 0.88f, 0.7f), 0.5f, 0f, new Color(1f, 0.78f, 0.52f) * 3f),
            Glass = glass,
            Tile = CityBuilder.Solid("Lux Green Tile", new Color(0.09f, 0.24f, 0.2f), 0.92f),
            Frame = CityBuilder.Solid("Lux Black Frame", new Color(0.04f, 0.04f, 0.05f), 0.6f, 0.7f),
            Art = new[]
            {
                CityBuilder.Solid("Lux Art Blue", new Color(0.14f, 0.24f, 0.55f), 0.3f),
                CityBuilder.Solid("Lux Art Gold", new Color(0.8f, 0.6f, 0.25f), 0.5f, 0.6f),
                CityBuilder.Solid("Lux Art Cream", new Color(0.9f, 0.86f, 0.78f), 0.3f),
            },
            LightColor = new Color(1f, 0.82f, 0.62f), LightIntensity = 2f,
        };

        styles[(int)InteriorTheme.Natural] = new Style
        {
            Theme = InteriorTheme.Natural,
            Tagline = "Warm natural · oak, terracotta & plants",
            OneBhkSqFt = 650, TwoBhkSqFt = 1050, Height = 2.9f,
            Floor = CityBuilder.Textured("Nat Oak Floor", ProceduralTextures.Planks("Nat Oak Floor", new Color(0.8f, 0.64f, 0.46f), 6, 51), 3.6f, 0.25f),
            Counter = CityBuilder.Solid("Nat Butcher Block", new Color(0.6f, 0.42f, 0.25f), 0.4f),
            Wall = CityBuilder.Solid("Nat Warm White", new Color(0.96f, 0.93f, 0.87f), 0.1f),
            Accent = CityBuilder.Solid("Nat Terracotta Wall", new Color(0.72f, 0.38f, 0.26f), 0.1f),
            Ceiling = CityBuilder.Solid("Nat Ceiling", new Color(0.97f, 0.95f, 0.9f), 0.1f),
            Sofa = CityBuilder.Solid("Nat Oatmeal Linen", new Color(0.82f, 0.76f, 0.66f), 0.1f),
            Cushion = CityBuilder.Solid("Nat Mustard Cushion", new Color(0.74f, 0.56f, 0.22f), 0.1f),
            Linen = white,
            Duvet = CityBuilder.Solid("Nat Sage Duvet", new Color(0.6f, 0.68f, 0.55f), 0.1f),
            Headboard = CityBuilder.Solid("Nat Rattan", new Color(0.72f, 0.56f, 0.36f), 0.2f),
            Wood = CityBuilder.Solid("Nat Light Oak", new Color(0.72f, 0.55f, 0.36f), 0.3f),
            Cabinet = CityBuilder.Solid("Nat Sage Cabinet", new Color(0.5f, 0.58f, 0.48f), 0.3f),
            Metal = CityBuilder.Solid("Nat Matte Black", new Color(0.1f, 0.1f, 0.1f), 0.4f, 0.5f),
            Rug = CityBuilder.Textured("Nat Rug", ProceduralTextures.Paving("Nat Rug", new Color(0.82f, 0.72f, 0.58f), 8, 53), 1.2f, 0.05f),
            Leaves = CityBuilder.Solid("Nat Leaves", new Color(0.24f, 0.48f, 0.18f), 0.25f),
            Pot = CityBuilder.Solid("Nat Terracotta Pot", new Color(0.66f, 0.38f, 0.26f), 0.2f),
            Screen = screen,
            Lamp = CityBuilder.Solid("Nat Lamp", new Color(1f, 0.9f, 0.75f), 0.5f, 0f, new Color(1f, 0.82f, 0.6f) * 3f),
            Glass = glass,
            Tile = CityBuilder.Solid("Nat White Zellige", new Color(0.93f, 0.92f, 0.88f), 0.9f),
            Frame = CityBuilder.Solid("Nat Wood Frame", new Color(0.55f, 0.4f, 0.25f), 0.35f),
            Art = new[]
            {
                CityBuilder.Solid("Nat Art Terracotta", new Color(0.78f, 0.45f, 0.3f), 0.3f),
                CityBuilder.Solid("Nat Art Olive", new Color(0.45f, 0.5f, 0.28f), 0.3f),
                CityBuilder.Solid("Nat Art Sand", new Color(0.88f, 0.8f, 0.64f), 0.3f),
            },
            LightColor = new Color(1f, 0.85f, 0.68f), LightIntensity = 1.8f,
        };

        styles[(int)InteriorTheme.Industrial] = new Style
        {
            Theme = InteriorTheme.Industrial,
            Tagline = "Industrial loft · concrete, brick & steel",
            OneBhkSqFt = 600, TwoBhkSqFt = 980, Height = 3f,
            Floor = CityBuilder.Textured("Loft Concrete Floor", ProceduralTextures.Noise("Loft Concrete", new Color(0.5f, 0.5f, 0.5f), new Color(0.64f, 0.63f, 0.61f), 10, 61), 4f, 0.6f),
            Counter = CityBuilder.Solid("Loft Stainless", new Color(0.75f, 0.76f, 0.78f), 0.7f, 1f),
            Wall = CityBuilder.Solid("Loft Gray Wall", new Color(0.8f, 0.8f, 0.78f), 0.15f),
            Accent = CityBuilder.Textured("Loft Brick Wall", ProceduralTextures.Bricks("Loft Brick", new Color(0.56f, 0.29f, 0.2f), new Color(0.72f, 0.7f, 0.66f), 62), 2f, 0.15f),
            Ceiling = CityBuilder.Solid("Loft Dark Ceiling", new Color(0.13f, 0.13f, 0.14f), 0.2f),
            Sofa = CityBuilder.Solid("Loft Cognac Leather", new Color(0.5f, 0.26f, 0.12f), 0.55f),
            Cushion = CityBuilder.Solid("Loft Charcoal Cushion", new Color(0.2f, 0.2f, 0.22f), 0.15f),
            Linen = CityBuilder.Solid("Loft Gray Linen", new Color(0.75f, 0.76f, 0.78f), 0.1f),
            Duvet = CityBuilder.Solid("Loft Charcoal Duvet", new Color(0.24f, 0.25f, 0.27f), 0.1f),
            Headboard = CityBuilder.Solid("Loft Black Steel", new Color(0.07f, 0.07f, 0.08f), 0.5f, 0.8f),
            Wood = CityBuilder.Solid("Loft Reclaimed Wood", new Color(0.42f, 0.3f, 0.2f), 0.25f),
            Cabinet = CityBuilder.Solid("Loft Concrete Cabinet", new Color(0.42f, 0.43f, 0.45f), 0.35f),
            Metal = CityBuilder.Solid("Loft Steel", new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.8f),
            Rug = CityBuilder.Textured("Loft Rug", ProceduralTextures.Noise("Loft Rug", new Color(0.17f, 0.17f, 0.19f), new Color(0.29f, 0.28f, 0.28f), 30, 63), 1f, 0.05f),
            Leaves = CityBuilder.Solid("Loft Leaves", new Color(0.18f, 0.4f, 0.2f), 0.25f),
            Pot = CityBuilder.Solid("Loft Concrete Pot", new Color(0.55f, 0.55f, 0.54f), 0.2f),
            Screen = screen,
            Lamp = CityBuilder.Solid("Loft Lamp", new Color(0.95f, 0.97f, 1f), 0.5f, 0f, new Color(0.9f, 0.95f, 1f) * 3f),
            Glass = glass,
            Tile = CityBuilder.Solid("Loft Subway Tile", new Color(0.9f, 0.9f, 0.9f), 0.9f),
            Frame = CityBuilder.Solid("Loft Steel Frame", new Color(0.06f, 0.06f, 0.07f), 0.5f, 0.8f),
            Partition = CityBuilder.Transparent("Loft Glass Partition", new Color(0.8f, 0.9f, 0.95f, 0.14f), 0.6f),
            Strip = CityBuilder.Solid("Loft LED Strip", new Color(0.8f, 0.9f, 1f), 0.5f, 0f, new Color(0.6f, 0.85f, 1f) * 4f),
            Art = new[]
            {
                CityBuilder.Solid("Loft Art Cyan", new Color(0.12f, 0.55f, 0.65f), 0.3f),
                CityBuilder.Solid("Loft Art Black", new Color(0.06f, 0.06f, 0.07f), 0.5f),
                CityBuilder.Solid("Loft Art White", new Color(0.92f, 0.92f, 0.9f), 0.3f),
            },
            LightColor = new Color(0.95f, 0.96f, 1f), LightIntensity = 1.7f,
        };
    }

    public static ApartmentInfo Build(Transform building, string name, Vector3 localPosition, float localYaw, bool twoBhk, InteriorTheme theme)
    {
        s = styles[(int)theme];
        Height = s.Height;

        var root = new GameObject(name).transform;
        root.SetParent(building, false);
        root.localPosition = localPosition;
        root.localRotation = Quaternion.Euler(0f, localYaw, 0f);

        var lights = new GameObject("Interior Lights");
        lights.transform.SetParent(root, false);

        float width = twoBhk ? 14f : 10f, depth = twoBhk ? 9f : 8f;
        Shell(root, width, depth);
        Vector3 eye, lookAt;
        if (twoBhk) TwoBhk(root, lights.transform, out eye, out lookAt);
        else OneBhk(root, lights.transform, out eye, out lookAt);
        ThemeDetails(root, lights.transform, width, depth, twoBhk);

        string rooms = twoBhk ? "Living & dining · 2 bedrooms · kitchen" : "Living & dining · 1 bedroom · kitchen";
        int sqft = twoBhk ? s.TwoBhkSqFt : s.OneBhkSqFt;
        var info = new ApartmentInfo
        {
            Label = twoBhk ? "2 BHK" : "1 BHK",
            Details = $"{s.Tagline}\n{rooms}\n{sqft:N0} sq ft",
            Entry = Marker(root, "Entry Camera", new Vector3(0f, 1.8f, -13f), new Vector3(0f, 1.4f, 3f)),
            Interior = Marker(root, "Interior Camera", eye, lookAt),
            Lights = lights,
            Root = root,
            Size = new Vector3(width, Height, depth),
        };
        lights.SetActive(false); // the director switches them on for the apartment being shown
        return info;
    }

    static Transform Marker(Transform root, string name, Vector3 position, Vector3 lookAt)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(root, false);
        marker.localPosition = position;
        marker.localRotation = Quaternion.LookRotation(lookAt - position);
        return marker;
    }

    // ---------------------------------------------------------------- Layouts

    static void OneBhk(Transform root, Transform lights, out Vector3 eye, out Vector3 lookAt)
    {
        // Living/dining across the front, bedroom back-left, open kitchen back-right.
        WallAlongX(root, -5f, 1f, 4.6f, -1.2f);
        WallAlongZ(root, 4.6f, 8f, 1f);
        Box(root, "Accent Wall", new Vector3(-5f + 0.07f, Height / 2f, 2.3f), new Vector3(0.02f, Height, 4.6f), s.Accent);

        Living(root, -5f, -1.1f, 2.4f, false);
        Plant(Group(root, "Plant", new Vector3(-4.5f, 0f, 0.6f), 0f), 1.1f);
        FloorLamp(Group(root, "Floor Lamp", new Vector3(-0.4f, 0f, 3.9f), 0f));
        Art(root, new Vector3(-3.2f, 1.6f, 4.6f - 0.09f), new Vector2(1.3f, 0.85f), s.Art[0]);

        DiningTable(Group(root, "Dining", new Vector3(2.9f, 0f, 2.5f), 0f), 1.6f, 2);
        Pendant(root, new Vector3(2.9f, Height, 2.5f));
        Kitchen(Group(root, "Kitchen", new Vector3(3.05f, 0f, 7.7f), 0f), 3.9f);
        Island(Group(root, "Island", new Vector3(3f, 0f, 5.9f), 0f), 1.8f);
        Plant(Group(root, "Plant", new Vector3(4.5f, 0f, 0.6f), 0f), 0.9f);

        Bed(Group(root, "Bed", new Vector3(-2f, 0f, 6.95f), 0f), 1.6f);
        Nightstand(Group(root, "Nightstand", new Vector3(-3.25f, 0f, 7.7f), 0f));
        Nightstand(Group(root, "Nightstand", new Vector3(-0.75f, 0f, 7.7f), 0f));
        Wardrobe(Group(root, "Wardrobe", new Vector3(-4.65f, 0f, 5.6f), -90f), 1.6f);
        Art(root, new Vector3(-2f, 1.75f, 8f - 0.09f), new Vector2(1.4f, 0.6f), s.Art[1]);

        CeilingLight(root, lights, new Vector3(-2.4f, 0f, 2.4f), 5f);
        CeilingLight(root, lights, new Vector3(2.9f, 0f, 2.5f), 3.5f);
        CeilingLight(root, lights, new Vector3(3f, 0f, 6.4f), 3.5f);
        CeilingLight(root, lights, new Vector3(-2f, 0f, 6.2f), 3.5f);

        eye = new Vector3(2.6f, 1.55f, 0.9f);
        lookAt = new Vector3(-2.2f, 1.15f, 6f);
    }

    static void TwoBhk(Transform root, Transform lights, out Vector3 eye, out Vector3 lookAt)
    {
        // Living/dining across the front; two bedrooms and an open kitchen behind.
        WallAlongX(root, -7f, 3f, 5f, -4f, 0.5f);
        WallAlongZ(root, 5f, 9f, -2f);
        WallAlongZ(root, 5f, 9f, 3f);
        Box(root, "Accent Wall", new Vector3(-7f + 0.07f, Height / 2f, 2.5f), new Vector3(0.02f, Height, 5f), s.Accent);

        Living(root, -7f, -2.6f, 2.6f, true);
        Plant(Group(root, "Plant", new Vector3(-6.5f, 0f, 0.6f), 0f), 1.2f);
        Plant(Group(root, "Plant", new Vector3(6.5f, 0f, 0.6f), 0f), 1f);
        FloorLamp(Group(root, "Floor Lamp", new Vector3(-1.7f, 0f, 4.3f), 0f));
        Art(root, new Vector3(-5.2f, 1.6f, 5f - 0.09f), new Vector2(1.6f, 1f), s.Art[2]);

        DiningTable(Group(root, "Dining", new Vector3(3.9f, 0f, 2.6f), 0f), 2.1f, 3);
        Pendant(root, new Vector3(3.4f, Height, 2.6f));
        Pendant(root, new Vector3(4.4f, Height, 2.6f));
        Kitchen(Group(root, "Kitchen", new Vector3(5.05f, 0f, 8.7f), 0f), 3.9f);
        Island(Group(root, "Island", new Vector3(5f, 0f, 6.6f), 0f), 1.8f);

        Bed(Group(root, "Master Bed", new Vector3(-4.5f, 0f, 7.95f), 0f), 1.8f);
        Nightstand(Group(root, "Nightstand", new Vector3(-5.85f, 0f, 8.7f), 0f));
        Nightstand(Group(root, "Nightstand", new Vector3(-3.15f, 0f, 8.7f), 0f));
        Wardrobe(Group(root, "Wardrobe", new Vector3(-6.65f, 0f, 6.1f), -90f), 1.8f);
        Art(root, new Vector3(-4.5f, 1.75f, 9f - 0.09f), new Vector2(1.5f, 0.6f), s.Art[1]);

        Bed(Group(root, "Second Bed", new Vector3(0.2f, 0f, 7.95f), 0f), 1.4f);
        Nightstand(Group(root, "Nightstand", new Vector3(-1.4f, 0f, 8.7f), 0f));
        Desk(Group(root, "Desk", new Vector3(2.55f, 0f, 6.1f), 90f));
        Art(root, new Vector3(0.2f, 1.75f, 9f - 0.09f), new Vector2(1.1f, 0.6f), s.Art[0]);

        CeilingLight(root, lights, new Vector3(-4.4f, 0f, 2.6f), 5f);
        CeilingLight(root, lights, new Vector3(0.2f, 0f, 2.5f), 4f);
        CeilingLight(root, lights, new Vector3(3.9f, 0f, 2.6f), 3.5f);
        CeilingLight(root, lights, new Vector3(5f, 0f, 7f), 3.5f);
        CeilingLight(root, lights, new Vector3(-4.5f, 0f, 7f), 3.5f);
        CeilingLight(root, lights, new Vector3(0.5f, 0f, 7f), 3.5f);

        eye = new Vector3(4.2f, 1.55f, 0.9f);
        lookAt = new Vector3(-3f, 1.15f, 6.5f);
    }

    /// <summary>
    /// The living area between the feature wall at <paramref name="wallX"/> and the sofa line at <paramref name="sofaX"/>,
    /// arranged differently per theme.
    /// </summary>
    static void Living(Transform root, float wallX, float sofaX, float z, bool large)
    {
        float mid = (wallX + 0.4f + sofaX) / 2f;
        float sofaLength = large ? 2.8f : 2.4f;
        switch (s.Theme)
        {
            case InteriorTheme.Luxury:
                // Formal: TV on the navy feature wall, velvet sofa facing it, a pair of armchairs and a round table.
                Rug(root, new Vector3(mid, 0f, z), new Vector2(3.6f, 3f));
                TvUnit(Group(root, "TV Unit", new Vector3(wallX + 0.25f, 0f, z), -90f));
                Sofa(Group(root, "Velvet Sofa", new Vector3(sofaX, 0f, z), 90f), sofaLength);
                Armchair(Group(root, "Armchair", new Vector3(mid, 0f, z - 1.65f), 180f));
                Armchair(Group(root, "Armchair", new Vector3(mid, 0f, z + 1.65f), 0f));
                Part(root, "Coffee Table", MeshLibrary.Cylinder(0.55f, 0.04f, 32), s.Counter, new Vector3(mid, 0.42f, z));
                Part(root, "Coffee Table Base", MeshLibrary.Cylinder(0.12f, 0.4f, 16), s.Metal, new Vector3(mid, 0.2f, z));
                break;

            case InteriorTheme.Natural:
                // Conversational: two linen sofas facing each other, a bookshelf on the terracotta wall, a pouf.
                Rug(root, new Vector3(mid, 0f, z), new Vector2(3.4f, 3.4f));
                Bookshelf(Group(root, "Bookshelf", new Vector3(wallX + 0.2f, 0f, z), -90f), large ? 2.4f : 2f);
                Sofa(Group(root, "Linen Sofa", new Vector3(mid, 0f, z - 1.45f), 180f), 2.2f);
                Sofa(Group(root, "Linen Sofa", new Vector3(mid, 0f, z + 1.45f), 0f), 2.2f);
                CoffeeTable(Group(root, "Coffee Table", new Vector3(mid, 0f, z), 0f));
                Part(root, "Pouf", MeshLibrary.Cylinder(0.3f, 0.4f, 20), s.Cushion, new Vector3(mid + 1.3f, 0.2f, z));
                break;

            default:
                // Loft: L-shaped leather sectional facing a low steel TV console.
                Rug(root, new Vector3(mid, 0f, z), new Vector2(3.8f, 3f));
                TvUnit(Group(root, "TV Console", new Vector3(wallX + 0.25f, 0f, z), -90f));
                Sofa(Group(root, "Leather Sectional", new Vector3(sofaX, 0f, z), 90f), sofaLength);
                Box(root, "Sectional Chaise", new Vector3(sofaX - 0.95f, 0.22f, z + sofaLength / 2f - 0.45f), new Vector3(1.9f, 0.44f, 0.9f), s.Sofa);
                CoffeeTable(Group(root, "Coffee Table", new Vector3(mid - 0.2f, 0f, z - 0.3f), 90f));
                break;
        }
    }

    /// <summary>Architectural details that make each theme read differently at a glance.</summary>
    static void ThemeDetails(Transform root, Transform lights, float width, float depth, bool twoBhk)
    {
        float w = width / 2f;
        switch (s.Theme)
        {
            case InteriorTheme.Luxury:
                // Walnut slats over the navy feature wall, a brass chandelier over the dining table.
                float accentDepth = twoBhk ? 5f : 4.6f;
                for (float z = 0.2f; z < accentDepth - 0.1f; z += 0.26f)
                    Box(root, "Wall Slat", new Vector3(-w + 0.1f, Height / 2f, z), new Vector3(0.05f, Height - 0.1f, 0.09f), s.Wood);
                Chandelier(root, twoBhk ? new Vector3(3.9f, Height, 2.6f) : new Vector3(2.9f, Height, 2.5f));
                break;

            case InteriorTheme.Natural:
                // Exposed oak beams, and plants everywhere: big floor plants, a row on the sill, hanging planters.
                for (float z = 1f; z < depth - 0.5f; z += 1.3f)
                    Box(root, "Ceiling Beam", new Vector3(0f, Height - 0.12f, z), new Vector3(width, 0.22f, 0.16f), s.Wood);
                for (float x = -w + 1.2f; x < w - 1f; x += 1.6f)
                    Plant(Group(root, "Sill Plant", new Vector3(x, 0.35f, 0.1f), 0f), 0.35f);
                var bigPlants = twoBhk
                    ? new[] { new Vector2(-w + 0.6f, depth - 0.6f), new Vector2(w - 0.6f, 4.2f), new Vector2(-1.8f, 0.7f) }
                    : new[] { new Vector2(-w + 0.6f, depth - 0.6f), new Vector2(w - 0.6f, 4.2f) }; // a third would block the 1 BHK's opening view
                foreach (var spot in bigPlants)
                    Plant(Group(root, "Big Plant", new Vector3(spot.x, 0f, spot.y), 0f), 1.5f);
                foreach (float x in twoBhk ? new[] { -5.5f, -1.2f, 6f } : new[] { -3.8f, 0.6f, 4.4f })
                    HangingPlant(root, new Vector3(x, Height, 0.9f));
                break;

            default:
                // Dark ceiling with exposed ducts and track lights, a steel shelving unit, an LED strip over the brick.
                foreach (float z in new[] { 2.6f, depth - 1.6f })
                    Box(root, "Duct", new Vector3(0f, Height - 0.26f, z), new Vector3(width - 0.4f, 0.3f, 0.42f), s.Metal);
                foreach (float x in new[] { -w / 2f, w / 2f })
                {
                    Box(root, "Track Rail", new Vector3(x, Height - 0.06f, depth / 2f), new Vector3(0.05f, 0.05f, depth - 1f), s.Metal);
                    for (float z = 1f; z < depth - 0.8f; z += 1.4f)
                        Box(root, "Track Spot", new Vector3(x, Height - 0.16f, z), new Vector3(0.12f, 0.16f, 0.12f), s.Lamp);
                }
                float brickDepth = twoBhk ? 5f : 4.6f;
                Box(root, "LED Strip", new Vector3(-w + 0.1f, Height - 0.05f, brickDepth / 2f), new Vector3(0.04f, 0.04f, brickDepth - 0.2f), s.Strip);
                Shelving(Group(root, "Steel Shelving", twoBhk ? new Vector3(-2.2f, 0f, 4.75f) : new Vector3(0.4f, 0f, 4.35f), 0f));
                break;
        }
    }

    // ---------------------------------------------------------------- Shell

    static void Shell(Transform root, float width, float depth)
    {
        float w = width / 2f, t = WallThickness;
        Box(root, "Floor", new Vector3(0f, 0.05f, depth / 2f), new Vector3(width, 0.1f, depth), s.Floor);
        Box(root, "Ceiling", new Vector3(0f, Height + 0.05f, depth / 2f), new Vector3(width, 0.1f, depth), s.Ceiling);
        Box(root, "Back Wall", new Vector3(0f, Height / 2f, depth + t / 2f), new Vector3(width + 2f * t, Height, t), s.Wall);
        Box(root, "Side Wall", new Vector3(-w - t / 2f, Height / 2f, depth / 2f), new Vector3(t, Height, depth), s.Wall);
        Box(root, "Side Wall", new Vector3(w + t / 2f, Height / 2f, depth / 2f), new Vector3(t, Height, depth), s.Wall);

        // Floor-to-ceiling window wall: sill, header, mullions, and a faint glass pane. Frames differ per theme:
        // slim black for Luxury, chunky oak for Natural, black steel with a transom bar for Industrial.
        const float sill = 0.35f, header = 0.3f;
        float glassHeight = Height - sill - header;
        float mullion = s.Theme == InteriorTheme.Natural ? 0.12f : 0.06f;
        Box(root, "Window Sill", new Vector3(0f, sill / 2f, 0f), new Vector3(width + 2f * t, sill, 0.2f), s.Theme == InteriorTheme.Natural ? s.Wood : s.Wall);
        Box(root, "Window Header", new Vector3(0f, Height - header / 2f, 0f), new Vector3(width + 2f * t, header, 0.2f), s.Wall);
        int panes = Mathf.CeilToInt(width / (s.Theme == InteriorTheme.Industrial ? 1.6f : 2.2f));
        for (int i = 0; i <= panes; i++)
            Box(root, "Mullion", new Vector3(-w + i * width / panes, sill + glassHeight / 2f, 0f), new Vector3(mullion, glassHeight, 0.12f), s.Frame);
        if (s.Theme == InteriorTheme.Industrial)
            Box(root, "Transom", new Vector3(0f, 2.15f, 0f), new Vector3(width, 0.06f, 0.12f), s.Frame);
        Box(root, "Glass", new Vector3(0f, sill + glassHeight / 2f, 0f), new Vector3(width, glassHeight, 0.02f), s.Glass);
        if (s.Theme != InteriorTheme.Industrial)
            foreach (float side in new[] { -1f, 1f })
                Box(root, "Curtain", new Vector3(side * (w - 0.35f), Height / 2f, 0.25f), new Vector3(0.55f, Height - 0.15f, 0.12f),
                    s.Theme == InteriorTheme.Luxury ? s.Duvet : s.Linen);
    }

    /// <summary>Partition running along x at depth z, with door openings centred on the given x positions.</summary>
    static void WallAlongX(Transform root, float x0, float x1, float z, params float[] doors) =>
        Partition(root, x0, x1, doors, (a, b) => new Vector3((a + b) / 2f, 0f, z), length => new Vector3(length, 0f, WallThickness));

    /// <summary>Partition running along z at x, with door openings centred on the given z positions.</summary>
    static void WallAlongZ(Transform root, float z0, float z1, float x, params float[] doors) =>
        Partition(root, z0, z1, doors, (a, b) => new Vector3(x, 0f, (a + b) / 2f), length => new Vector3(WallThickness, 0f, length));

    static void Partition(Transform root, float start, float end, float[] doors,
        System.Func<float, float, Vector3> center, System.Func<float, Vector3> size)
    {
        // Industrial lofts get steel-framed glass partitions instead of plastered walls.
        bool glass = s.Partition != null;
        var material = glass ? s.Partition : s.Wall;
        float cursor = start;
        void Segment(float a, float b, float bottom, float top)
        {
            if (b - a < 0.01f) return;
            var sz = size(b - a);
            sz.y = top - bottom;
            var c = center(a, b);
            Box(root, "Partition", c + Vector3.up * (bottom + sz.y / 2f), sz, material);
            if (!glass) return;
            var rail = new Vector3(sz.x, 0.06f, sz.z) + new Vector3(0.02f, 0f, 0.02f);
            Box(root, "Partition Frame", c + Vector3.up * (bottom + 0.03f), rail, s.Frame);
            Box(root, "Partition Frame", c + Vector3.up * (top - 0.03f), rail, s.Frame);
            float length = b - a;
            for (float d = 0f; d <= length + 0.01f; d += Mathf.Max(length / Mathf.Ceil(length / 1.2f), 0.6f))
            {
                var post = center(a + d, a + d);
                Box(root, "Partition Post", post + Vector3.up * (bottom + sz.y / 2f), new Vector3(0.05f, sz.y, 0.05f), s.Frame);
            }
        }
        foreach (float door in doors)
        {
            Segment(cursor, door - DoorWidth / 2f, 0f, Height);
            Segment(door - DoorWidth / 2f, door + DoorWidth / 2f, DoorHeight, Height); // lintel
            cursor = door + DoorWidth / 2f;
        }
        Segment(cursor, end, 0f, Height);
    }

    // ---------------------------------------------------------------- Furniture (fronts face local -z)

    static void Sofa(Transform g, float length)
    {
        Box(g, "Seat", new Vector3(0f, 0.22f, 0f), new Vector3(length, 0.44f, 0.9f), s.Sofa);
        Box(g, "Back", new Vector3(0f, 0.62f, 0.35f), new Vector3(length, 0.42f, 0.2f), s.Sofa);
        foreach (float side in new[] { -1f, 1f })
            Box(g, "Arm", new Vector3(side * (length / 2f - 0.1f), 0.32f, 0f), new Vector3(0.2f, 0.64f, 0.9f), s.Sofa);
        if (s.Theme == InteriorTheme.Luxury)
            foreach (float side in new[] { -1f, 1f })
                Part(g, "Leg", MeshLibrary.Cylinder(0.03f, 0.08f, 8), s.Metal, new Vector3(side * (length / 2f - 0.15f), 0.04f, -0.35f));
        int cushions = Mathf.Max(1, Mathf.FloorToInt(length / 0.9f));
        for (int i = 0; i < cushions; i++)
        {
            float x = -length / 2f + 0.35f + (length - 0.7f) * (cushions == 1 ? 0.5f : i / (cushions - 1f));
            Box(g, "Cushion", new Vector3(x, 0.66f, 0.2f), new Vector3(0.42f, 0.38f, 0.13f), s.Cushion);
        }
    }

    static void Armchair(Transform g)
    {
        Box(g, "Seat", new Vector3(0f, 0.25f, 0f), new Vector3(0.85f, 0.42f, 0.8f), s.Cushion);
        Box(g, "Back", new Vector3(0f, 0.65f, 0.32f), new Vector3(0.85f, 0.5f, 0.16f), s.Cushion);
        foreach (float side in new[] { -1f, 1f })
            Box(g, "Arm", new Vector3(side * 0.37f, 0.4f, 0f), new Vector3(0.12f, 0.36f, 0.8f), s.Cushion);
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Part(g, "Leg", MeshLibrary.Cylinder(0.025f, 0.08f, 8), s.Metal, new Vector3(corner.x * 0.36f, 0.04f, corner.y * 0.33f));
    }

    static void CoffeeTable(Transform g)
    {
        Box(g, "Top", new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.05f, 0.6f), s.Wood);
        Box(g, "Base", new Vector3(0f, 0.19f, 0f), new Vector3(1f, 0.36f, 0.42f), s.Theme == InteriorTheme.Natural ? s.Wood : s.Metal);
    }

    static void TvUnit(Transform g)
    {
        var body = s.Theme == InteriorTheme.Industrial ? s.Metal : s.Wood;
        Box(g, "Cabinet", new Vector3(0f, 0.22f, 0f), new Vector3(2f, 0.44f, 0.4f), body);
        Box(g, "Screen", new Vector3(0f, 1.3f, 0.12f), new Vector3(1.6f, 0.92f, 0.05f), s.Screen);
        Plant(Group(g, "Small Plant", new Vector3(0.75f, 0.44f, 0f), 0f), 0.45f);
    }

    static void Bookshelf(Transform g, float width)
    {
        Box(g, "Back", new Vector3(0f, 1.1f, 0.15f), new Vector3(width, 2.2f, 0.03f), s.Wood);
        foreach (float side in new[] { -1f, 1f })
            Box(g, "Side", new Vector3(side * width / 2f, 1.1f, 0f), new Vector3(0.04f, 2.2f, 0.32f), s.Wood);
        var random = new System.Random(7);
        for (int shelf = 0; shelf < 5; shelf++)
        {
            float y = 0.05f + shelf * 0.5f;
            Box(g, "Shelf", new Vector3(0f, y, 0f), new Vector3(width, 0.04f, 0.32f), s.Wood);
            if (shelf == 4) break;
            for (float x = -width / 2f + 0.15f; x < width / 2f - 0.15f; x += 0.08f + (float)random.NextDouble() * 0.05f)
            {
                float h = 0.22f + (float)random.NextDouble() * 0.14f;
                Box(g, "Book", new Vector3(x, y + 0.02f + h / 2f, 0.02f), new Vector3(0.05f, h, 0.24f), s.Art[random.Next(s.Art.Length)]);
            }
        }
    }

    static void Shelving(Transform g)
    {
        const float width = 1.8f, height = 2.2f;
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box(g, "Post", new Vector3(corner.x * width / 2f, height / 2f, corner.y * 0.18f), new Vector3(0.04f, height, 0.04f), s.Metal);
        for (int shelf = 0; shelf < 5; shelf++)
        {
            float y = 0.1f + shelf * 0.52f;
            Box(g, "Shelf", new Vector3(0f, y, 0f), new Vector3(width, 0.04f, 0.38f), s.Wood);
            if (shelf == 4) break;
            Box(g, "Box", new Vector3(-0.5f + shelf * 0.25f, y + 0.14f, 0f), new Vector3(0.36f, 0.24f, 0.28f), s.Art[shelf % s.Art.Length]);
            Plant(Group(g, "Shelf Plant", new Vector3(0.55f - shelf * 0.2f, y + 0.02f, 0f), 0f), 0.3f);
        }
    }

    static void Rug(Transform root, Vector3 center, Vector2 size) =>
        Box(root, "Rug", center + Vector3.up * 0.11f, new Vector3(size.x, 0.02f, size.y), s.Rug);

    static void DiningTable(Transform g, float length, int seatsPerSide)
    {
        // Marble-topped for Luxury, solid oak for Natural, reclaimed wood on steel for Industrial.
        var top = s.Theme == InteriorTheme.Luxury ? s.Counter : s.Wood;
        var legs = s.Theme == InteriorTheme.Natural ? s.Wood : s.Metal;
        Box(g, "Top", new Vector3(0f, 0.75f, 0f), new Vector3(length, 0.05f, 0.95f), top);
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box(g, "Leg", new Vector3(corner.x * (length / 2f - 0.08f), 0.37f, corner.y * 0.38f), new Vector3(0.06f, 0.73f, 0.06f), legs);
        for (int i = 0; i < seatsPerSide; i++)
        {
            float x = (i - (seatsPerSide - 1) / 2f) * (length / seatsPerSide);
            Chair(Group(g, "Chair", new Vector3(x, 0f, -0.7f), 180f));
            Chair(Group(g, "Chair", new Vector3(x, 0f, 0.7f), 0f));
        }
    }

    static void Chair(Transform g)
    {
        Box(g, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.45f, 0.05f, 0.45f), s.Theme == InteriorTheme.Industrial ? s.Sofa : s.Cushion);
        Box(g, "Back", new Vector3(0f, 0.72f, 0.2f), new Vector3(0.45f, 0.45f, 0.04f), s.Theme == InteriorTheme.Luxury ? s.Sofa : s.Wood);
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box(g, "Leg", new Vector3(corner.x * 0.19f, 0.22f, corner.y * 0.19f), new Vector3(0.035f, 0.44f, 0.035f), s.Theme == InteriorTheme.Natural ? s.Wood : s.Metal);
    }

    static void Pendant(Transform root, Vector3 ceilingPoint)
    {
        // Rattan drum for Natural, brass dome for Luxury, steel cage for Industrial.
        var shade = s.Theme == InteriorTheme.Natural ? s.Headboard : s.Theme == InteriorTheme.Luxury ? s.Metal : s.Frame;
        float radius = s.Theme == InteriorTheme.Natural ? 0.32f : 0.22f;
        Part(root, "Pendant Cord", MeshLibrary.Cylinder(0.01f, 0.9f, 6), s.Metal, ceilingPoint + Vector3.down * 0.45f);
        Part(root, "Pendant Shade", MeshLibrary.Cylinder(radius, 0.24f, 20), shade, ceilingPoint + Vector3.down * 1f);
        Part(root, "Pendant Bulb", MeshLibrary.Cylinder(radius - 0.04f, 0.02f, 20), s.Lamp, ceilingPoint + Vector3.down * 1.12f);
    }

    static void Chandelier(Transform root, Vector3 ceilingPoint)
    {
        var center = ceilingPoint + Vector3.down * 0.7f;
        Part(root, "Chandelier Rod", MeshLibrary.Cylinder(0.015f, 0.68f, 8), s.Metal, ceilingPoint + Vector3.down * 0.34f);
        Part(root, "Chandelier Ring", MeshLibrary.Cylinder(0.45f, 0.025f, 32), s.Metal, center);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            var p = center + new Vector3(Mathf.Cos(a) * 0.45f, -0.05f, Mathf.Sin(a) * 0.45f);
            Part(root, "Chandelier Bulb", MeshLibrary.Sphere(), s.Lamp, p, 0f, Vector3.one * 0.08f);
        }
    }

    static void HangingPlant(Transform root, Vector3 ceilingPoint)
    {
        Part(root, "Hanger", MeshLibrary.Cylinder(0.008f, 0.65f, 6), s.Metal, ceilingPoint + Vector3.down * 0.32f);
        Part(root, "Hanging Pot", MeshLibrary.Cylinder(0.16f, 0.2f, 16), s.Pot, ceilingPoint + Vector3.down * 0.72f);
        Part(root, "Hanging Leaves", MeshLibrary.Sphere(), s.Leaves, ceilingPoint + Vector3.down * 0.86f, 0f, new Vector3(0.5f, 0.55f, 0.5f));
    }

    static void Kitchen(Transform g, float length)
    {
        float cabinets = length - 0.85f; // leave room for the fridge
        float cx = -length / 2f + cabinets / 2f;
        Box(g, "Base Cabinets", new Vector3(cx, 0.44f, 0f), new Vector3(cabinets, 0.88f, 0.6f), s.Cabinet);
        Box(g, "Countertop", new Vector3(cx, 0.9f, -0.01f), new Vector3(cabinets, 0.04f, 0.62f), s.Counter);
        Box(g, "Backsplash", new Vector3(cx, 1.2f, 0.29f), new Vector3(cabinets, 0.56f, 0.02f), s.Tile);
        switch (s.Theme)
        {
            case InteriorTheme.Natural:
                // Open oak shelves with jars instead of wall cabinets.
                foreach (float y in new[] { 1.65f, 2.05f })
                {
                    Box(g, "Open Shelf", new Vector3(cx, y, 0.15f), new Vector3(cabinets, 0.04f, 0.28f), s.Wood);
                    for (float x = cx - cabinets / 2f + 0.3f; x < cx + cabinets / 2f - 0.2f; x += 0.45f)
                        Part(g, "Jar", MeshLibrary.Cylinder(0.07f, 0.18f, 12), s.Tile, new Vector3(x, y + 0.11f, 0.15f));
                }
                break;
            case InteriorTheme.Industrial:
                // Steel range hood and a single open shelf.
                Box(g, "Range Hood", new Vector3(cx - 0.6f, 1.95f, 0.1f), new Vector3(0.7f, 0.5f, 0.45f), s.Counter);
                Box(g, "Open Shelf", new Vector3(cx + 0.6f, 1.75f, 0.15f), new Vector3(cabinets - 1.4f, 0.04f, 0.28f), s.Wood);
                break;
            default:
                // Full-height lacquer cabinets.
                Box(g, "Wall Cabinets", new Vector3(cx, 1.95f, 0.12f), new Vector3(cabinets, 0.9f, 0.35f), s.Cabinet);
                break;
        }
        Box(g, "Hob", new Vector3(cx - 0.6f, 0.925f, -0.05f), new Vector3(0.6f, 0.01f, 0.5f), s.Screen);
        Box(g, "Sink", new Vector3(cx + 0.7f, 0.925f, -0.05f), new Vector3(0.7f, 0.01f, 0.42f), s.Counter);
        Box(g, "Fridge", new Vector3(length / 2f - 0.42f, 1f, 0f), new Vector3(0.8f, 2f, 0.62f), s.Theme == InteriorTheme.Luxury ? s.Cabinet : s.Counter);
    }

    static void Island(Transform g, float length)
    {
        Box(g, "Island", new Vector3(0f, 0.44f, 0f), new Vector3(length, 0.88f, 0.85f), s.Cabinet);
        Box(g, "Island Top", new Vector3(0f, 0.9f, 0f), new Vector3(length + 0.1f, 0.04f, 0.95f), s.Counter);
        for (float x = -length / 2f + 0.4f; x <= length / 2f - 0.3f; x += 0.65f)
        {
            Part(g, "Stool", MeshLibrary.Cylinder(0.18f, 0.05f, 16), s.Theme == InteriorTheme.Industrial ? s.Sofa : s.Cushion, new Vector3(x, 0.7f, -0.75f));
            Part(g, "Stool Leg", MeshLibrary.Cylinder(0.03f, 0.68f, 8), s.Metal, new Vector3(x, 0.34f, -0.75f));
        }
    }

    static void Bed(Transform g, float width)
    {
        // Tall velvet headboard for Luxury, low rattan for Natural, black steel frame for Industrial.
        float headboard = s.Theme == InteriorTheme.Luxury ? 1.4f : s.Theme == InteriorTheme.Natural ? 0.9f : 1.05f;
        Box(g, "Frame", new Vector3(0f, 0.15f, 0f), new Vector3(width + 0.1f, 0.3f, 2.1f), s.Theme == InteriorTheme.Industrial ? s.Metal : s.Wood);
        Box(g, "Mattress", new Vector3(0f, 0.42f, 0f), new Vector3(width, 0.24f, 2f), s.Linen);
        Box(g, "Duvet", new Vector3(0f, 0.56f, -0.3f), new Vector3(width + 0.04f, 0.06f, 1.42f), s.Duvet);
        Box(g, "Throw", new Vector3(0f, 0.6f, -0.75f), new Vector3(width + 0.06f, 0.04f, 0.45f), s.Cushion);
        Box(g, "Headboard", new Vector3(0f, 0.3f + headboard / 2f, 1.03f), new Vector3(width + 0.2f, headboard, 0.08f), s.Headboard);
        int pillows = width > 1.2f ? 2 : 1;
        for (int i = 0; i < pillows; i++)
        {
            float x = pillows == 1 ? 0f : (i == 0 ? -1f : 1f) * width / 4f;
            Box(g, "Pillow", new Vector3(x, 0.62f, 0.72f), new Vector3(width / pillows - 0.15f, 0.14f, 0.4f), s.Linen);
        }
    }

    static void Nightstand(Transform g)
    {
        Box(g, "Nightstand", new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.4f), s.Wood);
        Part(g, "Lamp Base", MeshLibrary.Cylinder(0.06f, 0.25f, 12), s.Metal, new Vector3(0f, 0.62f, 0f));
        Part(g, "Lamp Shade", MeshLibrary.Cylinder(0.14f, 0.2f, 16), s.Lamp, new Vector3(0f, 0.84f, 0f));
    }

    static void Wardrobe(Transform g, float width)
    {
        Box(g, "Wardrobe", new Vector3(0f, 1.1f, 0f), new Vector3(width, 2.2f, 0.6f), s.Theme == InteriorTheme.Luxury ? s.Cabinet : s.Wood);
        Box(g, "Door Gap", new Vector3(0f, 1.1f, -0.301f), new Vector3(0.01f, 2.1f, 0.01f), s.Frame);
        foreach (float side in new[] { -1f, 1f })
            Box(g, "Handle", new Vector3(side * 0.06f, 1.1f, -0.32f), new Vector3(0.02f, 0.4f, 0.03f), s.Metal);
    }

    static void Desk(Transform g)
    {
        Box(g, "Desk Top", new Vector3(0f, 0.74f, 0f), new Vector3(1.3f, 0.04f, 0.6f), s.Wood);
        foreach (float side in new[] { -1f, 1f })
            Box(g, "Desk Side", new Vector3(side * 0.62f, 0.37f, 0f), new Vector3(0.04f, 0.72f, 0.58f), s.Theme == InteriorTheme.Industrial ? s.Metal : s.Wood);
        Box(g, "Monitor", new Vector3(0f, 1.05f, 0.18f), new Vector3(0.6f, 0.36f, 0.03f), s.Screen);
        Chair(Group(g, "Desk Chair", new Vector3(0f, 0f, -0.55f), 180f));
    }

    static void Plant(Transform g, float scale)
    {
        Part(g, "Pot", MeshLibrary.Cylinder(0.2f * scale, 0.4f * scale, 16), s.Pot, new Vector3(0f, 0.2f * scale, 0f));
        Part(g, "Leaves", MeshLibrary.Sphere(), s.Leaves, new Vector3(0f, 0.75f * scale, 0f), 0f, new Vector3(0.65f, 0.85f, 0.65f) * scale);
    }

    static void FloorLamp(Transform g)
    {
        Part(g, "Lamp Pole", MeshLibrary.Cylinder(0.02f, 1.6f, 8), s.Metal, new Vector3(0f, 0.8f, 0f));
        Part(g, "Lamp Foot", MeshLibrary.Cylinder(0.18f, 0.03f, 16), s.Metal, new Vector3(0f, 0.015f, 0f));
        Part(g, "Lamp Shade", MeshLibrary.Cylinder(0.22f, 0.32f, 20), s.Lamp, new Vector3(0f, 1.6f, 0f));
    }

    static void Art(Transform root, Vector3 position, Vector2 size, Material canvas)
    {
        var g = Group(root, "Wall Art", position, 0f);
        Box(g, "Frame", Vector3.zero, new Vector3(size.x + 0.06f, size.y + 0.06f, 0.03f), s.Theme == InteriorTheme.Luxury ? s.Metal : s.Frame);
        Box(g, "Canvas", new Vector3(0f, 0f, -0.01f), new Vector3(size.x, size.y, 0.03f), canvas);
    }

    static void CeilingLight(Transform root, Transform lights, Vector3 floorPoint, float range)
    {
        Part(root, "Ceiling Light", MeshLibrary.Cylinder(0.16f, 0.03f, 20), s.Lamp, new Vector3(floorPoint.x, Height - 0.015f, floorPoint.z));
        var light = new GameObject("Room Light").AddComponent<Light>();
        light.transform.SetParent(lights, false);
        // Hung well below the ceiling so it fills the room instead of making a hot spot on the ceiling.
        light.transform.localPosition = new Vector3(floorPoint.x, Height - 0.9f, floorPoint.z);
        light.type = LightType.Point;
        light.color = s.LightColor;
        light.range = range + 1f;
        light.intensity = s.LightIntensity;
        light.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- Helpers

    static Transform Group(Transform parent, string name, Vector3 position, float yaw)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = position;
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return g;
    }

    static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material) =>
        CityBuilder.Box(parent, name, center, size, material);

    static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, float yaw = 0f, Vector3? scale = null) =>
        CityBuilder.Part(parent, name, mesh, material, position, yaw, scale);
}
