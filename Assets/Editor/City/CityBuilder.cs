using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Facade = ProceduralTextures.FacadeStyle;

public struct BuildingInfo
{
    public string Name;
    public Color Accent;
    public Transform Root;
    public Vector3 Focus, CameraPosition;
    public ApartmentInfo[] Apartments;
    public float FrameHeight, FrameWidth; // size the building shot is framed to
}

/// <summary>
/// Procedurally generates the city: golden-hour lighting, street grid, context blocks, a plaza and park,
/// and the three hero buildings. Deterministic, so rebuilding always gives the same city.
/// </summary>
static class CityBuilder
{
    /// <summary>
    /// True: Building 2 is the 187 m "USA Building With Terrace" asset (if installed), with the plaza widened
    /// and the idle camera pulled back to fit it. False: the original procedural Terrace Center.
    /// </summary>
    const bool UseUsaBuildingForBuilding2 = false;

    static bool UsaBuilding2 => UseUsaBuildingForBuilding2 && UsaTerraceBuilding.IsAvailable;

    // The idle shot is pulled back and raised when the 187 m tower is in the middle.
    public static Vector3 IdleFocus => UsaBuilding2 ? new Vector3(0f, 62f, 22f) : new Vector3(0f, 34f, 22f);
    public static Vector3 IdleCamera => UsaBuilding2 ? new Vector3(0f, 140f, -330f) : new Vector3(0f, 98f, -225f);

    const string GeneratedFolder = "Assets/Generated";
    const string MaterialFolder = GeneratedFolder + "/Materials";
    const float RoadWidth = 14f;
    const float CurbHeight = 0.2f;

    // Road centre lines. The hero plaza sits between x = ±110 and z = -45..75.
    static readonly float[] XLines = { -510, -430, -350, -270, -190, -110, 110, 190, 270, 350, 430, 510 };
    static readonly float[] ZLines = { -285, -205, -125, -45, 75, 155, 235, 315, 395, 475 };

    static Palette p;
    static System.Random random;
    static Transform nightLightsRoot;
    static List<Light> nightLights;
    static List<Material> facades;
    static Material usaWindowGlow;
    static Transform nycRoot;

    static readonly DemoTouch.LightingState DayLook = new DemoTouch.LightingState
    {
        sunIntensity = 2.1f, sunColor = new Color(1f, 0.86f, 0.7f), sunEuler = new Vector3(27f, 38f, 0f),
        skyExposure = 1.3f, skyTint = new Color(0.4f, 0.48f, 0.62f), atmosphere = 0.85f,
        ambientSky = new Color(0.5f, 0.58f, 0.72f), ambientEquator = new Color(0.56f, 0.52f, 0.47f), ambientGround = new Color(0.22f, 0.2f, 0.18f),
        fogColor = new Color(0.74f, 0.73f, 0.72f), fogStart = 180f, fogEnd = 1100f,
    };

    // Moonlight from the same side as the sun so the camera-facing facades still read at night.
    static readonly DemoTouch.LightingState NightLook = new DemoTouch.LightingState
    {
        sunIntensity = 0.28f, sunColor = new Color(0.55f, 0.66f, 1f), sunEuler = new Vector3(42f, 20f, 0f),
        skyExposure = 0.16f, skyTint = new Color(0.16f, 0.22f, 0.42f), atmosphere = 0.45f,
        ambientSky = new Color(0.07f, 0.09f, 0.16f), ambientEquator = new Color(0.09f, 0.08f, 0.1f), ambientGround = new Color(0.03f, 0.03f, 0.035f),
        fogColor = new Color(0.06f, 0.07f, 0.11f), fogStart = 120f, fogEnd = 950f,
    };

    class Palette
    {
        public Material Asphalt, Paving, PlazaPaving, Grass, Water, Stone, Concrete, White, Silver, DarkMetal, Wood;
        public Material Marking, RoofDark, GlassRail, Trunk, LampGlow, AzureGlow, TealGlow, Beacon, Solar, CarGlass, HeadLight, TailLight;
        public Material Azure, Terrace, Helix, PodiumDark;
        public Material[] Leaves, CarPaint, Context, Podiums;
    }

    public static BuildingInfo[] Build(float fieldOfView, out DemoTouch.DayNightController dayNight)
    {
        random = new System.Random(2024);
        nightLights = new List<Light>();
        facades = new List<Material>();
        usaWindowGlow = null;
        ResetFolders();
        MeshLibrary.Begin();
        p = CreatePalette();

        var sun = SetupLighting(out var dayVolume, out var nightVolume);
        var city = new GameObject("City").transform;
        nightLightsRoot = new GameObject("Night Lights").transform;
        nightLightsRoot.SetParent(city, false);
        NycBuildings.Begin();
        PackTrees.Begin();
        nycRoot = new GameObject("NYC Buildings").transform;
        nycRoot.SetParent(city, false);

        var ground = new MeshBatch();
        ground.Box(new Vector3(0f, -0.1f, 0f), new Vector3(8000f, 0.2f, 8000f), p.Asphalt);
        ground.Flush("Ground", city);

        var streets = new MeshBatch();
        var context = new MeshBatch();
        var plaza = new MeshBatch();
        BuildRoads(streets);
        BuildBlocks(streets, context, plaza);
        BuildCars(streets);

        ApartmentBuilder.CreateMaterials();

        // Building 2 is the "USA Building With Terrace" asset when it's in the project; the procedural
        // Terrace Center is a fallback so the scene still builds without it.
        bool usaBuilding = UsaBuilding2;
        float sideX = usaBuilding ? 74f : 60f; // the side towers move out to make room for the wide USA building
        var heroes = new[]
        {
            AzureTower(city, new Vector3(-sideX, CurbHeight, 10f), plaza),
            usaBuilding
                ? TerraceTower(city, new Vector3(0f, CurbHeight, 28f))
                : TerraceCenter(city, new Vector3(0f, CurbHeight, 34f), plaza),
            HelixTower(city, new Vector3(sideX + 2f, CurbHeight, 6f), plaza),
        };
        // Yaw > 0 places the camera to the building's left (west), < 0 to its right.
        Frame(ref heroes[0], 128f, 34f, 24f, 9f, fieldOfView);
        if (usaBuilding) Frame(ref heroes[1], heroes[1].FrameHeight, heroes[1].FrameWidth, 14f, 8f, fieldOfView);
        else Frame(ref heroes[1], 54f, 48f, 12f, 16f, fieldOfView);
        Frame(ref heroes[2], 122f, 32f, -24f, 9f, fieldOfView);

        // A 1 BHK and a 2 BHK inside each building, on the side facing the plaza.
        heroes[0].Apartments = new[]
        {
            ApartmentBuilder.Build(heroes[0].Root, "1 BHK Apartment", new Vector3(0f, 26.6f, -9.65f), 0f, false, InteriorTheme.Luxury),
            ApartmentBuilder.Build(heroes[0].Root, "2 BHK Apartment", new Vector3(0f, 46.6f, -9.65f), 0f, true, InteriorTheme.Luxury),
        };
        if (!usaBuilding)
            heroes[1].Apartments = new[]
            {
                ApartmentBuilder.Build(heroes[1].Root, "1 BHK Apartment", new Vector3(-10f, 4.2f, -16.65f), 0f, false, InteriorTheme.Natural),
                ApartmentBuilder.Build(heroes[1].Root, "2 BHK Apartment", new Vector3(8f, 16.2f, -10.65f), 0f, true, InteriorTheme.Natural),
            };
        heroes[2].Apartments = new[] { HelixApartment(heroes[2].Root, 4, false), HelixApartment(heroes[2].Root, 11, true) };

        streets.Flush("Streetscape", city);
        context.Flush("Context Buildings", city);
        plaza.Flush("Plaza & Park", city);

        dayNight = new GameObject("Day Night").AddComponent<DemoTouch.DayNightController>();
        var probe = CreateReflectionProbe();
        dayNight.Configure(sun, dayVolume, nightVolume, probe, DayLook, NightLook, EmissiveGroups(), nightLights.ToArray());

        // Bake one reflection cubemap per look so glass reflects a lit city at night.
        dayNight.Apply(1f);
        var nightCubemap = BakeReflection(probe, "City Reflection Night");
        dayNight.Apply(0f);
        var dayCubemap = BakeReflection(probe, "City Reflection Day");
        dayNight.SetReflections(dayCubemap, nightCubemap);
        dayNight.Apply(0f);
        EditorUtility.SetDirty(dayNight);

        AssetDatabase.SaveAssets();
        return heroes;
    }

    /// <summary>Groups every renderer by the emissive material it uses, with that material's day and night glow.</summary>
    static DemoTouch.EmissiveGroup[] EmissiveGroups()
    {
        var looks = new Dictionary<Material, (Color day, Color night)>
        {
            [p.LampGlow] = (new Color(1f, 0.8f, 0.5f) * 0.3f, new Color(1f, 0.8f, 0.5f) * 10f),
            [p.AzureGlow] = (new Color(0.3f, 0.6f, 1f) * 2f, new Color(0.3f, 0.6f, 1f) * 9f),
            [p.TealGlow] = (new Color(0.2f, 0.9f, 0.75f) * 2f, new Color(0.2f, 0.9f, 0.75f) * 9f),
            [p.Beacon] = (new Color(1f, 0.05f, 0.05f) * 4f, new Color(1f, 0.05f, 0.05f) * 14f),
            [p.HeadLight] = (Color.white * 0.2f, new Color(1f, 0.95f, 0.85f) * 8f),
            [p.TailLight] = (new Color(1f, 0.05f, 0.03f) * 0.2f, new Color(1f, 0.05f, 0.03f) * 5f),
        };
        foreach (var facade in facades)
            looks[facade] = (Color.black, Color.white * 1.15f);
        if (usaWindowGlow != null)
            looks[usaWindowGlow] = (Color.black, new Color(1f, 0.72f, 0.42f) * 0.9f);

        // A renderer can use several materials (the USA building's sections do); the property block
        // reaches all of them, but only materials with _EMISSION enabled actually glow.
        var renderers = new Dictionary<Material, List<Renderer>>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            foreach (var material in r.sharedMaterials)
                if (material != null && looks.ContainsKey(material))
                {
                    if (!renderers.TryGetValue(material, out var list)) renderers[material] = list = new List<Renderer>();
                    list.Add(r);
                    break;
                }

        var groups = new List<DemoTouch.EmissiveGroup>();
        foreach (var pair in renderers)
            groups.Add(new DemoTouch.EmissiveGroup
            {
                name = pair.Key.name, day = looks[pair.Key].day, night = looks[pair.Key].night, renderers = pair.Value.ToArray(),
            });
        return groups.ToArray();
    }

    static Light NightLight(string name, LightType type, Vector3 position, Color color, float intensity, float range,
        Vector3? aimAt = null, float spotAngle = 60f)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(nightLightsRoot, false);
        light.transform.position = position;
        if (aimAt.HasValue) light.transform.rotation = Quaternion.LookRotation(aimAt.Value - position);
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.innerSpotAngle = spotAngle * 0.5f;
        light.shadows = LightShadows.None;
        nightLights.Add(light);
        return light;
    }

    static void Floodlight(Vector3 position, Vector3 aimAt, Color color, float intensity, float range, float angle) =>
        NightLight("Floodlight", LightType.Spot, position, color, intensity, range, aimAt, angle);

    // Must match the floor stack in HelixTower().
    const int HelixFloors = 28;
    const float HelixBaseY = 6.6f, HelixFloorHeight = 3.6f, HelixTwist = 3f;

    /// <summary>Apartment inside one of the Helix Tower's twisted floor plates, turned with that floor.</summary>
    static ApartmentInfo HelixApartment(Transform helix, int floor, bool twoBhk)
    {
        float size = Mathf.Lerp(24f, 17f, floor / (HelixFloors - 1f));
        float yaw = floor * HelixTwist;
        var position = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -size / 2f + 0.35f)
                       + Vector3.up * (HelixBaseY + floor * HelixFloorHeight + 0.02f);
        return ApartmentBuilder.Build(helix, twoBhk ? "2 BHK Apartment" : "1 BHK Apartment", position, yaw, twoBhk, InteriorTheme.Industrial);
    }

    /// <summary>Places a shot that fits the whole building on screen, seen from the given yaw and elevation.</summary>
    static void Frame(ref BuildingInfo b, float height, float width, float yaw, float elevation, float fov)
    {
        float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * 16f / 9f;
        float distance = Mathf.Max(height * 0.5f * 1.12f / tanV, width * 0.5f * 1.6f / tanH);
        b.Focus = b.Root.position + Vector3.up * height * 0.5f;
        b.CameraPosition = b.Focus + Quaternion.Euler(elevation, yaw, 0f) * Vector3.back * distance;
    }

    // ---------------------------------------------------------------- Setup

    static void ResetFolders()
    {
        AssetDatabase.DeleteAsset(GeneratedFolder);
        AssetDatabase.CreateFolder("Assets", "Generated");
        AssetDatabase.CreateFolder(GeneratedFolder, "Textures");
        AssetDatabase.CreateFolder(GeneratedFolder, "Materials");
    }

    static Palette CreatePalette()
    {
        var palette = new Palette
        {
            Asphalt = Textured("Asphalt", ProceduralTextures.Noise("Asphalt", new Color(0.13f, 0.135f, 0.145f), new Color(0.2f, 0.2f, 0.21f), 8, 3), 10f, 0.25f),
            Paving = Textured("Sidewalk", ProceduralTextures.Paving("Sidewalk", new Color(0.6f, 0.59f, 0.56f), 8, 5), 8f, 0.2f),
            PlazaPaving = Textured("Plaza Paving", ProceduralTextures.Paving("Plaza Paving", new Color(0.78f, 0.74f, 0.68f), 4, 9), 6f, 0.35f),
            Grass = Textured("Grass", ProceduralTextures.Noise("Grass", new Color(0.2f, 0.33f, 0.12f), new Color(0.33f, 0.45f, 0.17f), 12, 7), 6f, 0.05f),
            Water = Solid("Water", new Color(0.07f, 0.16f, 0.2f), 0.98f, 0.3f),
            Stone = Solid("Stone", new Color(0.72f, 0.69f, 0.64f), 0.35f),
            Concrete = Solid("Concrete", new Color(0.55f, 0.55f, 0.54f), 0.2f),
            White = Solid("White Metal", new Color(0.9f, 0.91f, 0.92f), 0.65f, 0.3f),
            Silver = Solid("Silver", new Color(0.78f, 0.8f, 0.83f), 0.8f, 0.9f),
            DarkMetal = Solid("Dark Metal", new Color(0.12f, 0.13f, 0.14f), 0.5f, 0.7f),
            Wood = Solid("Wood", new Color(0.45f, 0.3f, 0.18f), 0.3f),
            Marking = Solid("Road Marking", new Color(0.85f, 0.85f, 0.82f), 0.3f),
            RoofDark = Solid("Roof", new Color(0.24f, 0.24f, 0.25f), 0.15f),
            GlassRail = Transparent("Glass Railing", new Color(0.7f, 0.85f, 0.9f, 0.3f)),
            Trunk = Solid("Tree Trunk", new Color(0.3f, 0.22f, 0.15f), 0.1f),
            LampGlow = Solid("Lamp Glow", new Color(1f, 0.85f, 0.6f), 0.5f, 0f, new Color(1f, 0.8f, 0.5f) * 3f),
            AzureGlow = Solid("Azure Glow", new Color(0.4f, 0.7f, 1f), 0.5f, 0f, new Color(0.3f, 0.6f, 1f) * 4f),
            TealGlow = Solid("Teal Glow", new Color(0.3f, 0.95f, 0.8f), 0.5f, 0f, new Color(0.2f, 0.9f, 0.75f) * 4f),
            Beacon = Solid("Beacon", Color.red, 0.5f, 0f, new Color(1f, 0.05f, 0.05f) * 6f),
            Solar = Solid("Solar Panel", new Color(0.05f, 0.08f, 0.16f), 0.92f, 0.6f),
            CarGlass = Solid("Car Glass", new Color(0.05f, 0.06f, 0.08f), 0.95f, 0.5f),
            HeadLight = Solid("Headlight", new Color(0.9f, 0.9f, 0.85f), 0.9f, 0f, Color.white * 0.2f),
            TailLight = Solid("Taillight", new Color(0.5f, 0.02f, 0.02f), 0.9f, 0f, new Color(1f, 0.05f, 0.03f) * 0.2f),
            Leaves = new[]
            {
                Solid("Leaves A", new Color(0.18f, 0.34f, 0.12f), 0.15f),
                Solid("Leaves B", new Color(0.26f, 0.42f, 0.14f), 0.15f),
                Solid("Leaves C", new Color(0.36f, 0.45f, 0.16f), 0.15f),
            },
            CarPaint = new[]
            {
                Solid("Car White", new Color(0.85f, 0.86f, 0.87f), 0.85f, 0.4f),
                Solid("Car Black", new Color(0.05f, 0.05f, 0.06f), 0.85f, 0.5f),
                Solid("Car Silver", new Color(0.55f, 0.57f, 0.6f), 0.85f, 0.8f),
                Solid("Car Red", new Color(0.6f, 0.06f, 0.05f), 0.85f, 0.4f),
                Solid("Car Blue", new Color(0.08f, 0.2f, 0.45f), 0.85f, 0.4f),
                Solid("Car Taxi", new Color(0.95f, 0.72f, 0.1f), 0.8f, 0.3f),
            },
        };

        palette.Azure = FacadeMaterial(new Facade
        {
            Name = "Azure Curtain Wall", BayMeters = new Vector2(3f, 4f), Seed = 11,
            Wall = new Color(0.72f, 0.76f, 0.8f), Frame = new Color(0.82f, 0.84f, 0.87f),
            Glass = new Color(0.14f, 0.28f, 0.44f), Spandrel = new Color(0.12f, 0.18f, 0.26f),
            WindowWidth = 0.9f, WindowHeight = 0.76f, WindowBottom = 0.2f, GlassMetallic = 0.55f, LitChance = 0.06f,
        });
        palette.Terrace = FacadeMaterial(new Facade
        {
            Name = "Terrace Stone", BayMeters = new Vector2(4f, 4f), Seed = 12,
            Wall = new Color(0.86f, 0.76f, 0.62f), Frame = new Color(0.25f, 0.25f, 0.27f),
            Glass = new Color(0.12f, 0.15f, 0.18f), WindowWidth = 0.84f, WindowHeight = 0.52f, WindowBottom = 0.3f,
            Mullion = 0.02f, WallSmoothness = 0.25f, LitChance = 0.12f,
        });
        palette.Helix = FacadeMaterial(new Facade
        {
            Name = "Helix Glass", BayMeters = new Vector2(2.4f, 3.2f), Seed = 13,
            Wall = new Color(0.88f, 0.9f, 0.91f), Frame = new Color(0.85f, 0.88f, 0.9f),
            Glass = new Color(0.1f, 0.34f, 0.36f), WindowWidth = 0.95f, WindowHeight = 0.9f, WindowBottom = 0.05f,
            GlassMetallic = 0.5f, LitChance = 0.08f,
        });
        palette.PodiumDark = FacadeMaterial(new Facade
        {
            Name = "Dark Office", BayMeters = new Vector2(3f, 3.8f), Seed = 14,
            Wall = new Color(0.22f, 0.24f, 0.27f), Frame = new Color(0.3f, 0.32f, 0.35f),
            Glass = new Color(0.1f, 0.13f, 0.17f), Spandrel = new Color(0.16f, 0.18f, 0.21f),
            WindowWidth = 0.86f, WindowHeight = 0.7f, WindowBottom = 0.18f, LitChance = 0.15f,
        });

        var concrete = FacadeMaterial(new Facade
        {
            Name = "Concrete Punched", BayMeters = new Vector2(3.5f, 3.5f), Seed = 21,
            Wall = new Color(0.62f, 0.6f, 0.57f), Frame = new Color(0.45f, 0.45f, 0.45f),
            Glass = new Color(0.13f, 0.15f, 0.17f), WindowWidth = 0.5f, WindowHeight = 0.5f, WindowBottom = 0.3f, LitChance = 0.18f,
        });
        var brick = FacadeMaterial(new Facade
        {
            Name = "Brick", BayMeters = new Vector2(3f, 3.4f), Seed = 22, Brick = true,
            Wall = new Color(0.52f, 0.3f, 0.22f), Frame = new Color(0.85f, 0.82f, 0.75f),
            Glass = new Color(0.1f, 0.12f, 0.14f), WindowWidth = 0.45f, WindowHeight = 0.55f, WindowBottom = 0.28f,
            WallSmoothness = 0.15f, LitChance = 0.2f,
        });
        var warm = FacadeMaterial(new Facade
        {
            Name = "Warm Office", BayMeters = new Vector2(3.2f, 3.6f), Seed = 23,
            Wall = new Color(0.8f, 0.72f, 0.63f), Frame = new Color(0.4f, 0.38f, 0.36f),
            Glass = new Color(0.17f, 0.21f, 0.25f), WindowWidth = 0.7f, WindowHeight = 0.62f, WindowBottom = 0.22f,
            Mullion = 0.02f, LitChance = 0.14f,
        });
        var glassOffice = FacadeMaterial(new Facade
        {
            Name = "Blue Office", BayMeters = new Vector2(3f, 3.8f), Seed = 24,
            Wall = new Color(0.55f, 0.6f, 0.66f), Frame = new Color(0.6f, 0.64f, 0.68f),
            Glass = new Color(0.16f, 0.24f, 0.32f), Spandrel = new Color(0.2f, 0.25f, 0.3f),
            WindowWidth = 0.88f, WindowHeight = 0.66f, WindowBottom = 0.2f, GlassMetallic = 0.5f, LitChance = 0.1f,
        });
        var white = FacadeMaterial(new Facade
        {
            Name = "White Residential", BayMeters = new Vector2(3.6f, 3.2f), Seed = 25,
            Wall = new Color(0.86f, 0.86f, 0.84f), Frame = new Color(0.3f, 0.3f, 0.32f),
            Glass = new Color(0.12f, 0.14f, 0.17f), WindowWidth = 0.6f, WindowHeight = 0.6f, WindowBottom = 0.25f,
            Mullion = 0.02f, LitChance = 0.2f,
        });
        palette.Context = new[] { concrete, brick, warm, glassOffice, white, palette.PodiumDark };
        palette.Podiums = new[] { concrete, warm, palette.PodiumDark };
        return palette;
    }

    /// <summary>Creates the sun, sky and post-processing volumes. Their values are applied by the DayNightController.</summary>
    static Light SetupLighting(out Volume dayVolume, out Volume nightVolume)
    {
        var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "City Sky" };
        sky.SetFloat("_SunDisk", 2f);
        sky.SetFloat("_SunSize", 0.035f);
        sky.SetFloat("_SunSizeConvergence", 6f);
        // Matches the haze, so the horizon beyond the ground plane doesn't show as a brown band.
        sky.SetColor("_GroundColor", new Color(0.66f, 0.65f, 0.64f));
        AssetDatabase.CreateAsset(sky, $"{MaterialFolder}/Sky.mat");
        RenderSettings.skybox = sky;

        // Low sun from the south-west, behind the viewer, so the facades facing the camera are lit.
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        RenderSettings.sun = sun;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;

        dayVolume = new GameObject("Post Processing - Day").AddComponent<Volume>();
        dayVolume.isGlobal = true;
        dayVolume.sharedProfile = CreatePostProcessing("Day", exposure: 0.35f, contrast: 14f, saturation: 6f,
            temperature: 5f, tint: 6f, bloomThreshold: 1f, bloomIntensity: 0.6f, vignette: 0.24f);
        nightVolume = new GameObject("Post Processing - Night").AddComponent<Volume>();
        nightVolume.isGlobal = true;
        nightVolume.weight = 0f;
        nightVolume.sharedProfile = CreatePostProcessing("Night", exposure: 0.3f, contrast: 16f, saturation: 14f,
            temperature: -12f, tint: 2f, bloomThreshold: 0.9f, bloomIntensity: 0.9f, vignette: 0.32f);

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.shadowDistance < 400f)
        {
            urp.shadowDistance = 400f;
            EditorUtility.SetDirty(urp);
        }
        return sun;
    }

    static VolumeProfile CreatePostProcessing(string look, float exposure, float contrast, float saturation,
        float temperature, float tint, float bloomThreshold, float bloomIntensity, float vignette)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, $"{GeneratedFolder}/Post Processing {look}.asset");

        T Add<T>() where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var bloom = Add<Bloom>();
        bloom.threshold.Override(bloomThreshold);
        bloom.intensity.Override(bloomIntensity);
        bloom.scatter.Override(0.7f);
        var color = Add<ColorAdjustments>();
        color.postExposure.Override(exposure);
        color.contrast.Override(contrast);
        color.saturation.Override(saturation);
        var whiteBalance = Add<WhiteBalance>();
        whiteBalance.temperature.Override(temperature);
        whiteBalance.tint.Override(tint); // a little magenta counters the green cast ACES gives the horizon
        var vignetteEffect = Add<Vignette>();
        vignetteEffect.intensity.Override(vignette);
        vignetteEffect.smoothness.Override(0.45f);
        return profile;
    }

    static ReflectionProbe CreateReflectionProbe()
    {
        var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
        probe.transform.position = new Vector3(0f, 40f, -20f);
        probe.size = new Vector3(1400f, 500f, 1400f);
        probe.resolution = 256;
        probe.hdr = true;
        probe.mode = ReflectionProbeMode.Custom;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        return probe;
    }

    static Cubemap BakeReflection(ReflectionProbe probe, string name)
    {
        string path = $"{GeneratedFolder}/{name}.exr";
        if (!Lightmapping.BakeReflectionProbe(probe, path))
        {
            Debug.LogWarning($"Reflection probe bake failed for {name}; glass will reflect the skybox only.");
            return null;
        }
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Cubemap>(path);
    }

    // ---------------------------------------------------------------- Streets and blocks

    static void BuildRoads(MeshBatch b)
    {
        float xMin = XLines[0], xMax = XLines[XLines.Length - 1];
        float zMin = ZLines[0], zMax = ZLines[ZLines.Length - 1];

        // Dashed centre lines, broken at intersections.
        foreach (float x in XLines)
            for (float z = zMin; z < zMax; z += 9f)
                if (!NearLine(z, ZLines, RoadWidth * 0.5f + 2f))
                    b.Box(new Vector3(x, 0.02f, z), new Vector3(0.22f, 0.04f, 3f), p.Marking);
        foreach (float z in ZLines)
            for (float x = xMin; x < xMax; x += 9f)
                if (!NearLine(x, XLines, RoadWidth * 0.5f + 2f))
                    b.Box(new Vector3(x, 0.02f, z), new Vector3(3f, 0.04f, 0.22f), p.Marking);

        // Zebra crossings at the intersections near the plaza.
        foreach (float x in XLines)
        foreach (float z in ZLines)
        {
            if (Mathf.Abs(x) > 280f || z > 160f || z < -210f) continue;
            for (float s = -RoadWidth / 2f + 1f; s < RoadWidth / 2f; s += 1.3f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float offset = side * (RoadWidth / 2f + 2.5f);
                    b.Box(new Vector3(x + s, 0.025f, z + offset), new Vector3(0.6f, 0.05f, 3.2f), p.Marking);
                    b.Box(new Vector3(x + offset, 0.025f, z + s), new Vector3(3.2f, 0.05f, 0.6f), p.Marking);
                }
            }
        }

        // Street lights along the avenues framing the plaza.
        for (float x = -350f; x <= 350f; x += 32f)
            foreach (float z in new[] { -45f, 75f })
                if (!NearLine(x, XLines, 10f))
                {
                    StreetLight(b, new Vector3(x, CurbHeight, z - RoadWidth / 2f - 1f), Vector3.forward);
                    StreetLight(b, new Vector3(x + 16f, CurbHeight, z + RoadWidth / 2f + 1f), Vector3.back);
                }
        for (float z = -205f; z <= 235f; z += 32f)
            foreach (float x in new[] { -110f, 110f })
                if (!NearLine(z, ZLines, 10f))
                {
                    StreetLight(b, new Vector3(x - RoadWidth / 2f - 1f, CurbHeight, z), Vector3.right);
                    StreetLight(b, new Vector3(x + RoadWidth / 2f + 1f, CurbHeight, z + 16f), Vector3.left);
                }
    }

    static void StreetLight(MeshBatch b, Vector3 foot, Vector3 towardRoad)
    {
        var rotation = Quaternion.LookRotation(towardRoad);
        b.Add(MeshLibrary.Cylinder(0.12f, 8f, 8), p.DarkMetal, foot + Vector3.up * 4f, Quaternion.identity, Vector3.one);
        b.Add(MeshLibrary.Box(new Vector3(0.14f, 0.14f, 2.2f)), p.DarkMetal, foot + Vector3.up * 7.9f + towardRoad * 1.1f, rotation, Vector3.one);
        b.Add(MeshLibrary.Box(new Vector3(0.5f, 0.16f, 1f)), p.LampGlow, foot + Vector3.up * 7.75f + towardRoad * 2f, rotation, Vector3.one);
        NightLight("Street Light", LightType.Point, foot + Vector3.up * 7.2f + towardRoad * 2f,
            new Color(1f, 0.76f, 0.48f), 70f, 26f);
    }

    static void BuildBlocks(MeshBatch streets, MeshBatch context, MeshBatch plaza)
    {
        for (int xi = 0; xi < XLines.Length - 1; xi++)
        for (int zi = 0; zi < ZLines.Length - 1; zi++)
        {
            float x0 = XLines[xi] + RoadWidth / 2f, x1 = XLines[xi + 1] - RoadWidth / 2f;
            float z0 = ZLines[zi] + RoadWidth / 2f, z1 = ZLines[zi + 1] - RoadWidth / 2f;
            var area = Rect.MinMaxRect(x0, z0, x1, z1);
            bool hero = x0 < 0f && x1 > 0f && z0 < 0f && z1 > 0f;
            bool park = x0 < 0f && x1 > 0f && Mathf.Approximately(ZLines[zi + 1], -45f);

            var slab = hero ? p.PlazaPaving : p.Paving;
            streets.Box(new Vector3(area.center.x, CurbHeight / 2f, area.center.y), new Vector3(area.width, CurbHeight, area.height), slab);

            if (hero) HeroPlaza(plaza, area);
            else if (park) Park(plaza, area);
            else
            {
                bool nearby = Mathf.Abs(area.center.x) < 300f && area.center.y < 250f;
                if (nearby) StreetTrees(streets, area);
                ContextBlock(context, area, lowRise: z1 <= -45f);
            }
        }
    }

    static void ContextBlock(MeshBatch b, Rect area, bool lowRise)
    {
        const float nycShare = 0.9f; // share of taller lots that get an NYC-pack building
        bool nyc = !lowRise && NycBuildings.IsAvailable;

        var lots = new List<Rect>();
        int pattern = random.Next(3);
        // NYC buildings don't fit quarter-block lots, so blocks that can take them are mostly split
        // into one or two larger lots instead.
        if (nyc && pattern == 2 && random.NextDouble() < 0.75) pattern = random.Next(2);
        float hw = area.width / 2f, hh = area.height / 2f;
        if (pattern == 0) lots.Add(area);
        else if (pattern == 1)
        {
            lots.Add(new Rect(area.x, area.y, hw, area.height));
            lots.Add(new Rect(area.x + hw, area.y, hw, area.height));
        }
        else
        {
            for (int i = 0; i < 4; i++)
                lots.Add(new Rect(area.x + (i % 2) * hw, area.y + (i / 2) * hh, hw, hh));
        }

        foreach (var lot in lots)
        {
            var inner = Rect.MinMaxRect(lot.xMin + 3f, lot.yMin + 3f, lot.xMax - 3f, lot.yMax - 3f);
            float distance = Vector2.Distance(inner.center, new Vector2(0f, 20f));
            float height = lowRise
                ? Range(7f, 15f)
                : Mathf.Clamp(Mathf.Lerp(20f, 80f, distance / 480f) * Range(0.55f, 1.3f), 10f, 85f);
            float w = inner.width * Range(0.72f, 0.98f), d = inner.height * Range(0.72f, 0.98f);
            // Most taller lots get a building from the NYC pack (when it's installed); the rest stay procedural.
            // A 2 m setback (instead of 3 m) lets the 61 m-wide NYC blocks fit a full city block.
            var nycLot = Rect.MinMaxRect(lot.xMin + 2f, lot.yMin + 2f, lot.xMax - 2f, lot.yMax - 2f);
            if (nyc && random.NextDouble() < nycShare
                && NycBuildings.TryPlace(nycRoot, nycLot, CurbHeight, height, random, WindowGlow()))
                continue;

            var foot = new Vector3(inner.center.x, CurbHeight, inner.center.y);
            var style = p.Context[random.Next(p.Context.Length)];

            if (!lowRise && height > 30f && random.NextDouble() < 0.45)
            {
                const float podium = 8f;
                b.Box(foot + Vector3.up * podium / 2f, new Vector3(w, podium, d), p.Podiums[random.Next(p.Podiums.Length)]);
                b.Box(foot + Vector3.up * (podium + 0.2f), new Vector3(w + 0.4f, 0.4f, d + 0.4f), p.RoofDark);
                float tw = w * Range(0.55f, 0.75f), td = d * Range(0.55f, 0.75f);
                b.Box(foot + Vector3.up * (podium + (height - podium) / 2f), new Vector3(tw, height - podium, td), style);
                Roof(b, foot, tw, td, height);
            }
            else
            {
                b.Box(foot + Vector3.up * height / 2f, new Vector3(w, height, d), style);
                Roof(b, foot, w, d, height);
            }
        }
    }

    static void Roof(MeshBatch b, Vector3 foot, float w, float d, float top)
    {
        b.Box(foot + Vector3.up * (top + 0.3f), new Vector3(w + 0.5f, 0.6f, d + 0.5f), p.RoofDark);
        int units = random.Next(1, 4);
        for (int i = 0; i < units; i++)
        {
            var size = new Vector3(Range(2f, 5f), Range(1.2f, 2.6f), Range(2f, 5f));
            var offset = new Vector3(Range(-w / 2f + 3f, w / 2f - 3f), top + 0.6f + size.y / 2f, Range(-d / 2f + 3f, d / 2f - 3f));
            b.Box(foot + offset, size, random.NextDouble() < 0.5 ? p.Concrete : p.DarkMetal);
        }
        if (random.NextDouble() < 0.25)
            b.Add(MeshLibrary.Cylinder(1.6f, 3.2f, 16), p.DarkMetal,
                foot + new Vector3(Range(-w / 4f, w / 4f), top + 2.2f, Range(-d / 4f, d / 4f)), Quaternion.identity, Vector3.one);
    }

    static void StreetTrees(MeshBatch b, Rect area)
    {
        const float inset = 1.6f, spacing = 15f;
        for (float x = area.xMin + 6f; x < area.xMax - 4f; x += spacing)
        {
            Tree(b, new Vector3(x, CurbHeight, area.yMin + inset), Range(0.8f, 1.05f));
            Tree(b, new Vector3(x, CurbHeight, area.yMax - inset), Range(0.8f, 1.05f));
        }
        for (float z = area.yMin + 6f; z < area.yMax - 4f; z += spacing)
        {
            Tree(b, new Vector3(area.xMin + inset, CurbHeight, z), Range(0.8f, 1.05f));
            Tree(b, new Vector3(area.xMax - inset, CurbHeight, z), Range(0.8f, 1.05f));
        }
    }

    /// <summary>
    /// A tree whose size is relative to the procedural one (~6.6 m at scale 1). Uses the Trees_WorldSpace_FREE
    /// model when that asset is installed, otherwise a simple trunk-and-spheres tree.
    /// </summary>
    static void Tree(MeshBatch b, Vector3 foot, float scale)
    {
        if (PackTrees.Ready)
        {
            // The pack tree is 5.3 m tall; scale it to roughly match (with a little extra variety).
            float size = scale * 6.6f / PackTrees.Height * Range(0.9f, 1.25f);
            PackTrees.Add(b, foot, size, Quaternion.Euler(0f, Range(0f, 360f), 0f), random);
            return;
        }
        var leaves = p.Leaves[random.Next(p.Leaves.Length)];
        var yaw = Quaternion.Euler(0f, Range(0f, 360f), 0f);
        b.Add(MeshLibrary.Cylinder(0.22f, 3.2f, 8), p.Trunk, foot + Vector3.up * 1.6f * scale, Quaternion.identity, Vector3.one * scale);
        b.Add(MeshLibrary.Sphere(), leaves, foot + Vector3.up * 4.6f * scale, yaw, new Vector3(4.2f, 3.6f, 4.2f) * scale);
        b.Add(MeshLibrary.Sphere(), leaves, foot + yaw * new Vector3(0.9f, 6f, 0.4f) * scale, yaw, new Vector3(3f, 2.8f, 3f) * scale);
    }

    static void HeroPlaza(MeshBatch b, Rect area)
    {
        const float top = CurbHeight;

        // Reflecting pool in front of the Terrace Center, with benches along it.
        var pool = new Vector3(0f, top, UsaBuilding2 ? -21f : -18f);
        b.Box(pool + Vector3.up * 0.25f, new Vector3(74f, 0.5f, 16f), p.Stone);
        b.Box(pool + Vector3.up * 0.27f, new Vector3(71f, 0.5f, 13f), p.Water);
        for (float x = -30f; x <= 30f; x += 20f)
            NightLight("Pool Light", LightType.Point, pool + new Vector3(x, 1.2f, 0f), new Color(0.6f, 0.85f, 1f), 14f, 14f);
        for (float x = -30f; x <= 30f; x += 12f)
        {
            b.Box(new Vector3(x, top + 0.45f, pool.z - 10f), new Vector3(3.2f, 0.12f, 0.8f), p.Wood);
            b.Box(new Vector3(x, top + 0.2f, pool.z - 10f), new Vector3(2.6f, 0.4f, 0.5f), p.Stone);
        }

        // Lawns beside the pool and tree rows around the plaza edge.
        foreach (float side in new[] { -1f, 1f })
            b.Box(new Vector3(side * 62f, top + 0.15f, -22f), new Vector3(30f, 0.3f, 14f), p.Grass);
        for (float x = area.xMin + 6f; x < area.xMax - 4f; x += 12f)
            Tree(b, new Vector3(x, top, area.yMin + 4f), Range(0.9f, 1.1f));
        for (float z = area.yMin + 16f; z < area.yMax - 4f; z += 12f)
        {
            Tree(b, new Vector3(area.xMin + 4f, top, z), Range(0.9f, 1.1f));
            Tree(b, new Vector3(area.xMax - 4f, top, z), Range(0.9f, 1.1f));
        }
        foreach (float side in new[] { -1f, 1f })
            for (int i = 0; i < 3; i++)
                Tree(b, new Vector3(side * (52f + i * 9f), top + 0.3f, -22f + (i % 2) * 4f), Range(0.9f, 1.15f));
    }

    static void Park(MeshBatch b, Rect area)
    {
        const float top = CurbHeight;
        var c = new Vector3(area.center.x, top, area.center.y);
        b.Box(c + Vector3.up * 0.15f, new Vector3(area.width - 6f, 0.3f, area.height - 6f), p.Grass);
        b.Box(c + Vector3.up * 0.17f, new Vector3(area.width - 6f, 0.32f, 5f), p.PlazaPaving);
        b.Box(c + Vector3.up * 0.17f, new Vector3(5f, 0.32f, area.height - 6f), p.PlazaPaving);

        // Fountain at the crossing.
        b.Add(MeshLibrary.Cylinder(8f, 0.8f, 40), p.Stone, c + Vector3.up * 0.4f, Quaternion.identity, Vector3.one);
        b.Add(MeshLibrary.Cylinder(7.3f, 0.82f, 40), p.Water, c + Vector3.up * 0.42f, Quaternion.identity, Vector3.one);
        b.Add(MeshLibrary.Cylinder(1f, 3f, 16), p.Stone, c + Vector3.up * 1.5f, Quaternion.identity, Vector3.one);
        b.Add(MeshLibrary.Cylinder(2.6f, 0.3f, 24), p.Stone, c + Vector3.up * 3f, Quaternion.identity, Vector3.one);

        for (int placed = 0, attempts = 0; placed < 55 && attempts < 500; attempts++)
        {
            var pos = new Vector3(Range(area.xMin + 6f, area.xMax - 6f), top + 0.3f, Range(area.yMin + 6f, area.yMax - 6f));
            if (Mathf.Abs(pos.x - c.x) < 6f || Mathf.Abs(pos.z - c.z) < 6f) continue;
            Tree(b, pos, Range(0.8f, 1.3f));
            placed++;
        }
    }

    static void BuildCars(MeshBatch b)
    {
        var placed = new List<Vector3>();
        for (int attempts = 0; placed.Count < 150 && attempts < 2000; attempts++)
        {
            bool alongZ = random.NextDouble() < 0.5;
            float lane = random.NextDouble() < 0.5 ? -3.5f : 3.5f;
            Vector3 pos;
            float yaw;
            if (alongZ)
            {
                float x = XLines[random.Next(2, XLines.Length - 2)];
                float z = Range(-205f, 315f);
                if (NearLine(z, ZLines, 11f)) continue;
                pos = new Vector3(x + lane, 0f, z);
                yaw = lane > 0f ? 0f : 180f;
            }
            else
            {
                float z = ZLines[random.Next(1, ZLines.Length - 2)];
                float x = Range(-350f, 350f);
                if (NearLine(x, XLines, 11f)) continue;
                pos = new Vector3(x, 0f, z + lane);
                yaw = lane > 0f ? -90f : 90f;
            }
            if (placed.Exists(o => (o - pos).sqrMagnitude < 64f)) continue;
            placed.Add(pos);

            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var paint = p.CarPaint[random.Next(p.CarPaint.Length)];
            b.Add(MeshLibrary.Box(new Vector3(1.9f, 0.85f, 4.5f)), paint, pos + Vector3.up * 0.7f, rotation, Vector3.one);
            b.Add(MeshLibrary.Box(new Vector3(1.7f, 0.7f, 2.4f)), p.CarGlass, pos + Vector3.up * 1.45f + rotation * Vector3.back * 0.2f, rotation, Vector3.one);
            b.Add(MeshLibrary.Box(new Vector3(1.5f, 0.06f, 2.2f)), paint, pos + Vector3.up * 1.82f + rotation * Vector3.back * 0.2f, rotation, Vector3.one);
            foreach (float side in new[] { -0.65f, 0.65f })
            {
                b.Add(MeshLibrary.Box(new Vector3(0.38f, 0.16f, 0.06f)), p.HeadLight, pos + rotation * new Vector3(side, 0.85f, 2.25f), rotation, Vector3.one);
                b.Add(MeshLibrary.Box(new Vector3(0.38f, 0.14f, 0.06f)), p.TailLight, pos + rotation * new Vector3(side, 0.88f, -2.25f), rotation, Vector3.one);
            }
        }
    }

    // ---------------------------------------------------------------- Hero buildings

    static BuildingInfo AzureTower(Transform city, Vector3 at, MeshBatch details)
    {
        var root = Root("Building 1 - Azure Tower", city, at);
        Box(root, "Podium", new Vector3(0f, 5f, 0f), new Vector3(34f, 10f, 34f), p.PodiumDark);
        Box(root, "Podium Roof", new Vector3(0f, 10.3f, 0f), new Vector3(35f, 0.6f, 35f), p.Concrete);
        Box(root, "Entrance Canopy", new Vector3(0f, 4.4f, -19.5f), new Vector3(14f, 0.4f, 5f), p.White);

        Box(root, "Shaft", new Vector3(0f, 10.6f + 32f, 0f), new Vector3(20f, 64f, 20f), p.Azure);
        foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box(root, "Corner Fin", new Vector3(corner.x * 10.15f, 42.6f, corner.y * 10.15f), new Vector3(0.7f, 64f, 0.7f), p.Silver);

        float y = 74.6f;
        Box(root, "Setback Ring", new Vector3(0f, y + 0.3f, 0f), new Vector3(21f, 0.6f, 21f), p.Silver);
        y += 0.6f;
        Box(root, "Setback 1", new Vector3(0f, y + 8f, 0f), new Vector3(15f, 16f, 15f), p.Azure);
        y += 16f;
        Box(root, "Glow Ring", new Vector3(0f, y + 0.25f, 0f), new Vector3(15.8f, 0.5f, 15.8f), p.AzureGlow);
        y += 0.5f;
        Box(root, "Setback 2", new Vector3(0f, y + 6f, 0f), new Vector3(10f, 12f, 10f), p.Azure);
        y += 12f;
        Box(root, "Crown", new Vector3(0f, y + 0.5f, 0f), new Vector3(11f, 1f, 11f), p.AzureGlow);
        y += 1f;
        Part(root, "Spire", MeshLibrary.Cylinder(0.5f, 22f, 12), p.Silver, new Vector3(0f, y + 11f, 0f));
        Part(root, "Beacon", MeshLibrary.Sphere(), p.Beacon, new Vector3(0f, y + 22.4f, 0f), 0f, Vector3.one * 1.2f);

        // Cool floodlights on the podium roof wash each face of the shaft.
        foreach (var face in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            Floodlight(at + face * 13.5f + Vector3.up * 11.5f, at + face * 9f + Vector3.up * 70f,
                new Color(0.75f, 0.88f, 1f), 1800f, 120f, 34f);

        return new BuildingInfo { Name = "Azure Tower", Accent = new Color(0.25f, 0.55f, 0.95f), Root = root };
    }

    /// <summary>Building 2 from the "USA Building With Terrace" asset, with its apartments built into the facade facing the plaza.</summary>
    static BuildingInfo TerraceTower(Transform city, Vector3 at)
    {
        var root = Root("Building 2 - Terrace Tower", city, at);
        // Turned so the stepped wing and roof terrace face the plaza.
        var model = UsaTerraceBuilding.Build(root, 180f);
        var b = model.LocalBounds;

        var apartments = new[]
        {
            UsaApartment(root, model, model.Podium, -16f, 10.6f, false),
            UsaApartment(root, model, model.TowerSection(3), -14f, 78.2f, true),
        };

        // The pack's window panes get a copy of their material that DayNightController lights after dusk.
        var window = AssetDatabase.LoadAssetAtPath<Material>(UsaWindowMaterial);
        if (WindowGlow() != null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == window) { materials[i] = usaWindowGlow; changed = true; }
                if (changed) r.sharedMaterials = materials;
            }
        }

        // Warm floodlights in the plaza wash the brick facade at night.
        foreach (float x in new[] { -30f, -10f, 10f, 30f })
            Floodlight(at + new Vector3(x, 0.5f, b.min.z - 14f), at + new Vector3(x * 0.85f, 75f, b.min.z),
                new Color(1f, 0.78f, 0.52f), 6000f, 190f, 26f);

        return new BuildingInfo
        {
            Name = "Terrace Tower", Accent = new Color(0.95f, 0.62f, 0.25f), Root = root, Apartments = apartments,
            FrameHeight = b.size.y, FrameWidth = Mathf.Max(b.size.x, b.size.z),
        };
    }

    const string UsaWindowMaterial = "Assets/USA Building With Terrace (HDRP, PBR)/Materials/window.mat";

    /// <summary>
    /// Copy of the USA/NYC packs' shared window material with emission enabled, so DayNightController can light
    /// the windows after dusk. Created on first use; null if the packs aren't in the project.
    /// </summary>
    static Material WindowGlow()
    {
        if (usaWindowGlow != null) return usaWindowGlow;
        var window = AssetDatabase.LoadAssetAtPath<Material>(UsaWindowMaterial);
        if (window == null) return null;
        usaWindowGlow = new Material(window) { name = "USA Window Glow" };
        usaWindowGlow.SetColor("_EmissionColor", Color.white * 0.01f);
        usaWindowGlow.EnableKeyword("_EMISSION");
        usaWindowGlow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return Persist(usaWindowGlow);
    }

    /// <summary>Apartment set into the asset's south facade, hiding the section it's in while the camera is inside.</summary>
    static ApartmentInfo UsaApartment(Transform root, UsaTerraceBuilding.Result model, GameObject section, float x, float floorY, bool twoBhk)
    {
        float face = UsaTerraceBuilding.SouthFaceZ(root, model.Modules, x, floorY + 1.5f);
        if (float.IsNaN(face)) face = model.LocalBounds.min.z;
        var apartment = ApartmentBuilder.Build(root, twoBhk ? "2 BHK Apartment" : "1 BHK Apartment",
            new Vector3(x, floorY, face + 0.5f), 0f, twoBhk, InteriorTheme.Natural);
        apartment.Root.gameObject.AddComponent<DemoTouch.HideWhileCameraInside>()
            .Configure(section.GetComponentsInChildren<Renderer>(), apartment.Size, null);
        return apartment;
    }

    static BuildingInfo TerraceCenter(Transform city, Vector3 at, MeshBatch details)
    {
        var root = Root("Building 2 - Terrace Center", city, at);
        var tiers = new[] { new Vector2(48f, 34f), new Vector2(40f, 28f), new Vector2(32f, 22f), new Vector2(24f, 16f) };
        const float tierHeight = 12f;

        for (int i = 0; i < tiers.Length; i++)
        {
            float w = tiers[i].x, d = tiers[i].y, z = i * 3f, top = (i + 1) * tierHeight;
            Box(root, $"Tier {i + 1}", new Vector3(0f, i * tierHeight + tierHeight / 2f, z), new Vector3(w, tierHeight, d), p.Terrace);
            Box(root, "Roof Garden", new Vector3(0f, top + 0.2f, z), new Vector3(w - 1.2f, 0.4f, d - 1.2f), p.Grass);
            Box(root, "Balustrade", new Vector3(0f, top + 0.95f, z - d / 2f + 0.4f), new Vector3(w - 0.8f, 1.1f, 0.08f), p.GlassRail);
            foreach (float side in new[] { -1f, 1f })
                Box(root, "Balustrade", new Vector3(side * (w / 2f - 0.4f), top + 0.95f, z), new Vector3(0.08f, 1.1f, d - 0.8f), p.GlassRail);

            // Trees on the south-facing terrace strip left in front of the next tier.
            if (i < tiers.Length - 1)
                for (float x = -w / 2f + 4f; x <= w / 2f - 4f; x += 6f)
                    Tree(details, at + new Vector3(x, top + 0.4f, z - d / 2f + 3f), Range(0.4f, 0.5f));
        }

        float roof = tiers.Length * tierHeight + 0.4f;
        float roofZ = (tiers.Length - 1) * 3f;
        Box(root, "Rooftop Pavilion", new Vector3(0f, roof + 2f, roofZ + 3f), new Vector3(10f, 4f, 6f), p.PodiumDark);
        Box(root, "Pavilion Roof", new Vector3(0f, roof + 4.2f, roofZ + 3f), new Vector3(11f, 0.4f, 7f), p.White);
        for (int row = 0; row < 2; row++)
            for (float x = -9f; x <= 9f; x += 3.4f)
                details.Add(MeshLibrary.Box(new Vector3(3f, 0.1f, 1.8f)), p.Solar,
                    at + new Vector3(x, roof + 0.9f, roofZ - 5f + row * 2.6f), Quaternion.Euler(-22f, 0f, 0f), Vector3.one);
        Box(root, "Entrance Canopy", new Vector3(0f, 4.4f, -19f), new Vector3(16f, 0.4f, 4f), p.White);

        // Warm floodlights in the plaza light the stepped front and its terraces.
        foreach (float x in new[] { -18f, 0f, 18f })
            Floodlight(at + new Vector3(x, 0.5f, -30f), at + new Vector3(x * 0.6f, 22f, 0f),
                new Color(1f, 0.78f, 0.52f), 1400f, 90f, 52f);

        return new BuildingInfo { Name = "Terrace Center", Accent = new Color(0.95f, 0.62f, 0.25f), Root = root };
    }

    static BuildingInfo HelixTower(Transform city, Vector3 at, MeshBatch details)
    {
        var root = Root("Building 3 - Helix Tower", city, at);
        Part(root, "Lobby", MeshLibrary.Cylinder(16f, 6f, 48), p.Helix, new Vector3(0f, 3f, 0f));
        Part(root, "Lobby Roof", MeshLibrary.Cylinder(16.8f, 0.6f, 48), p.White, new Vector3(0f, 6.3f, 0f));

        // Each floor plate is rotated a little more than the one below it, giving the tower its twist.
        const int floors = HelixFloors;
        const float floorHeight = HelixFloorHeight, twist = HelixTwist;
        float baseY = HelixBaseY;
        for (int i = 0; i < floors; i++)
        {
            float t = i / (floors - 1f);
            float size = Mathf.Lerp(24f, 17f, t);
            float y = baseY + i * floorHeight;
            // Offset each floor's texture by one storey so the lit-window pattern doesn't repeat floor to floor.
            Part(root, $"Floor {i + 1}", MeshLibrary.Box(new Vector3(size, 3.2f, size), new Vector2(i * 7.2f, i * 3.2f)),
                p.Helix, new Vector3(0f, y + 1.6f, 0f), i * twist);
            Box(root, "Slab", new Vector3(0f, y + 3.4f, 0f), new Vector3(size + 1.4f, 0.4f, size + 1.4f), p.White, i * twist);
        }

        float top = baseY + floors * floorHeight;
        float finalYaw = (floors - 1) * twist, half = 17f / 2f;
        var rotation = Quaternion.Euler(0f, finalYaw, 0f);
        foreach (var corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) })
            Box(root, "Crown Fin", rotation * (corner * half) + Vector3.up * (top + 7f), new Vector3(0.6f, 14f, 0.6f), p.White, finalYaw);
        RingFrame(root, top + 13.8f, 18f, 0.5f, p.TealGlow, finalYaw);
        RingFrame(root, top + 7f, 17.6f, 0.4f, p.TealGlow, finalYaw);

        // Teal-white floodlights on the lobby roof run up the twisting faces.
        foreach (var face in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            Floodlight(at + face * 15f + Vector3.up * 7f, at + face * 10f + Vector3.up * 75f,
                new Color(0.7f, 1f, 0.95f), 1800f, 125f, 36f);
        NightLight("Crown Light", LightType.Point, at + Vector3.up * (top + 9f), new Color(0.3f, 1f, 0.85f), 60f, 30f);

        return new BuildingInfo { Name = "Helix Tower", Accent = new Color(0.2f, 0.82f, 0.7f), Root = root };
    }

    // ---------------------------------------------------------------- Helpers

    static Transform Root(string name, Transform parent, Vector3 position)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = position;
        return root;
    }

    /// <summary>Hollow square ring of four bars, centred on the building axis.</summary>
    static void RingFrame(Transform parent, float y, float size, float thickness, Material material, float yaw)
    {
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        float half = size / 2f;
        Box(parent, "Crown Ring", rotation * new Vector3(0f, 0f, -half) + Vector3.up * y, new Vector3(size, thickness, thickness), material, yaw);
        Box(parent, "Crown Ring", rotation * new Vector3(0f, 0f, half) + Vector3.up * y, new Vector3(size, thickness, thickness), material, yaw);
        Box(parent, "Crown Ring", rotation * new Vector3(-half, 0f, 0f) + Vector3.up * y, new Vector3(thickness, thickness, size), material, yaw);
        Box(parent, "Crown Ring", rotation * new Vector3(half, 0f, 0f) + Vector3.up * y, new Vector3(thickness, thickness, size), material, yaw);
    }

    internal static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, float yaw = 0f) =>
        Part(parent, name, MeshLibrary.Box(size), material, center, yaw);

    internal static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition,
        float yaw = 0f, Vector3? scale = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale ?? Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static bool NearLine(float value, float[] lines, float distance)
    {
        foreach (float line in lines)
            if (Mathf.Abs(value - line) < distance) return true;
        return false;
    }

    static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    // ---------------------------------------------------------------- Materials

    static Material Create(string name) =>
        new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };

    internal static Material CreateMaterial(string name) => Create(name);
    internal static Material PersistMaterial(Material material) => Persist(material);

    /// <summary>
    /// Saves a fully configured material. It must be configured first: URP validates keywords when the
    /// asset is imported, and an empty material at that moment loses _EMISSION and _METALLICSPECGLOSSMAP.
    /// </summary>
    static Material Persist(Material material)
    {
        AssetDatabase.CreateAsset(material, $"{MaterialFolder}/{material.name}.mat");
        return material;
    }

    internal static Material Solid(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
    {
        var m = Create(name);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            // URP's import validation keeps _EMISSION only on materials flagged emissive (no realtime GI is in use).
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return Persist(m);
    }

    internal static Material Textured(string name, Texture2D albedo, float tileMeters, float smoothness)
    {
        var m = Create(name);
        m.SetTexture("_BaseMap", albedo);
        m.SetTextureScale("_BaseMap", Vector2.one / tileMeters);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        return Persist(m);
    }

    static Material FacadeMaterial(Facade style)
    {
        var maps = ProceduralTextures.Facade(style);
        var m = Create(style.Name);
        m.SetTexture("_BaseMap", maps.Albedo);
        m.SetTextureScale("_BaseMap", new Vector2(1f / maps.TileMeters.x, 1f / maps.TileMeters.y));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_MetallicGlossMap", maps.Mask);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);
        // The emission map holds the night-time lit windows; DayNightController raises its strength after dusk.
        m.SetTexture("_EmissionMap", maps.Emission);
        // Not pure black, and flagged emissive: otherwise URP's import validation strips _EMISSION.
        // The day look drives it to black at runtime.
        m.SetColor("_EmissionColor", Color.white * 0.01f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        facades.Add(m);
        return Persist(m);
    }

    internal static Material Transparent(string name, Color color, float smoothness = 0.95f)
    {
        var m = Create(name);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return Persist(m);
    }
}
