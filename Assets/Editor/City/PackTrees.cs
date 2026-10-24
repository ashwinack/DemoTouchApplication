using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Trees from the "Trees_WorldSpace_FREE" asset. Its materials use Built-in pipeline shaders, which don't
/// render in URP, so URP/Lit copies are made here. The pack's shader picks a different quadrant of the 2×2
/// leaf atlas (and half of the bark texture) per tree; the same variety comes from one material per quadrant.
/// </summary>
static class PackTrees
{
    const string Root = "Assets/Trees_WorldSpace_FREE/";
    const string Prefab = Root + "Prefabs/SRP/MainTreeA_SRP.prefab";

    static Mesh mesh;
    static List<Material> barks, greens, accents;

    // Leaf atlas quadrants (set, quadrant 0-3 = top-left, top-right, bottom-left, bottom-right), sorted by
    // measured colour and coverage: full green crowns for most trees, autumn colours as occasional accents.
    static readonly (int set, int quadrant)[] GreenLeaves = { (0, 0), (1, 2), (2, 3), (2, 2), (2, 0) };
    static readonly (int set, int quadrant)[] AutumnLeaves = { (0, 2), (1, 3) };
    const float AutumnShare = 0.12f;

    public static bool IsAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null;

    /// <summary>Height of the unscaled tree mesh in metres.</summary>
    public const float Height = 5.3f;

    public static void Begin()
    {
        mesh = null;
        if (!IsAvailable) return;
        mesh = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponentInChildren<MeshFilter>().sharedMesh;

        barks = new List<Material>();
        foreach (var source in new[] { "TreeBarkAB", "TreeBarkCD" })
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/SRP/" + source + ".mat");
            for (int half = 0; half < 2; half++) // the bark texture holds two barks, top and bottom
                barks.Add(Bark($"{source} {half + 1}", original, new Vector2(0f, half * 0.5f)));
        }

        var sources = new[] { "TreeLeavesABCD_AS", "TreeLeavesEFGH_AS", "TreeLeavesIJKL_AS" };
        // The mesh's leaf UVs sit in the top-left quadrant; shifting by half reaches the other three.
        var offsets = new[] { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -0.5f), new Vector2(0.5f, -0.5f) };
        Material Make((int set, int quadrant) leaf)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/SRP/" + sources[leaf.set] + ".mat");
            return Leaves($"{sources[leaf.set]} {leaf.quadrant + 1}", original, offsets[leaf.quadrant], Color.white);
        }
        greens = new List<Material>();
        foreach (var leaf in GreenLeaves) greens.Add(Make(leaf));
        accents = new List<Material>();
        foreach (var leaf in AutumnLeaves) accents.Add(Make(leaf));
    }

    /// <summary>Adds one tree (bark + leaves submeshes) to <paramref name="batch"/>.</summary>
    public static void Add(MeshBatch batch, Vector3 foot, float scale, Quaternion rotation, System.Random random)
    {
        var size = Vector3.one * scale;
        batch.Add(mesh, barks[random.Next(barks.Count)], foot, rotation, size, 0);
        var pool = random.NextDouble() < AutumnShare ? accents : greens;
        batch.Add(mesh, pool[random.Next(pool.Count)], foot, rotation, size, 1);
    }

    public static bool Ready => mesh != null;

    static Material Bark(string name, Material original, Vector2 offset)
    {
        var m = CityBuilder.CreateMaterial(name);
        m.SetTexture("_BaseMap", original.GetTexture("_Albedo"));
        m.SetTextureOffset("_BaseMap", offset);
        m.SetColor("_BaseColor", Color.white);
        SetNormalMap(m, original.GetTexture("_NormalMap"));
        m.SetFloat("_Smoothness", 0.12f);
        m.SetFloat("_Metallic", 0f);
        return CityBuilder.PersistMaterial(m);
    }

    static Material Leaves(string name, Material original, Vector2 offset, Color tint)
    {
        var m = CityBuilder.CreateMaterial(name);
        m.SetTexture("_BaseMap", original.GetTexture("_Albedo_MaskA"));
        m.SetTextureOffset("_BaseMap", offset);
        m.SetColor("_BaseColor", tint);
        SetNormalMap(m, original.GetTexture("_NormalMap"));
        m.SetFloat("_Smoothness", 0.18f);
        m.SetFloat("_Metallic", 0f);

        // Leaf cards: cut out by the albedo alpha, and visible from both sides.
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", original.GetFloat("_Cutoff"));
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.renderQueue = (int)RenderQueue.AlphaTest;
        return CityBuilder.PersistMaterial(m);
    }

    static void SetNormalMap(Material m, Texture normal)
    {
        if (normal == null) return;
        // Only use it if Unity imports it as a normal map; the pack's import settings are left as they are.
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
        if (importer == null || importer.textureType != TextureImporterType.NormalMap) return;
        m.SetTexture("_BumpMap", normal);
        m.EnableKeyword("_NORMALMAP");
    }
}
