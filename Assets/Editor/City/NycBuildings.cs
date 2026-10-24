using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Places buildings from the "(HDRP) NYC-Like City Buildings Set (PBR)" asset into city lots: picks a variant
/// that fits the lot and is close to the wanted height, then strips the HDRP-only decal objects and swaps the
/// window panes for the material that lights up at night.
/// </summary>
static class NycBuildings
{
    const string Folder = "Assets/(HDRP) NYC-Like City Buildings Set (PBR)/Prefabs/Buildings";
    const string WindowMaterial = "Assets/USA Building With Terrace (HDRP, PBR)/Materials/window.mat";

    class Variant
    {
        public GameObject Prefab;
        public Vector3 Size;          // footprint x/z and height, unrotated
        public Vector3 CenterOffset;  // footprint centre relative to the prefab root
    }

    static List<Variant> variants;

    public static bool IsAvailable => AssetDatabase.IsValidFolder(Folder);

    /// <summary>Measures every building variant once per build (the prefabs have no usable bounds as assets).</summary>
    public static void Begin()
    {
        variants = new List<Variant>();
        if (!IsAvailable) return;
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!System.IO.Path.GetFileName(path).StartsWith("building_")) continue; // skip loose roof parts
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderers = probe.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                variants.Add(new Variant
                {
                    Prefab = prefab,
                    Size = b.size,
                    CenterOffset = new Vector3(b.center.x, b.min.y, b.center.z),
                });
            }
            Object.DestroyImmediate(probe);
        }
    }

    /// <summary>
    /// Places a variant inside <paramref name="lot"/> (x/z rect) at ground height <paramref name="groundY"/>.
    /// Returns false if nothing fits, so the caller can fall back to a procedural building.
    /// </summary>
    public static bool TryPlace(Transform parent, Rect lot, float groundY, float targetHeight, System.Random random, Material windowGlow)
    {
        if (variants == null || variants.Count == 0) return false;

        var candidates = new List<(Variant v, bool turned, float score)>();
        foreach (var v in variants)
        {
            if (v.Size.y > targetHeight * 1.35f) continue;
            foreach (bool turned in new[] { false, true })
            {
                float w = turned ? v.Size.z : v.Size.x, d = turned ? v.Size.x : v.Size.z;
                if (w > lot.width || d > lot.height) continue;
                // Prefer the wanted height and a footprint that fills the lot.
                float score = Mathf.Abs(v.Size.y - targetHeight) / targetHeight + (1f - w * d / (lot.width * lot.height)) * 0.6f;
                candidates.Add((v, turned, score));
            }
        }
        if (candidates.Count == 0) return false;

        var best = candidates.OrderBy(c => c.score).Take(3).ToList();
        var (variant, rotated, _) = best[random.Next(best.Count)];
        float yaw = (rotated ? 90f : 0f) + (random.NextDouble() < 0.5 ? 0f : 180f);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(variant.Prefab, parent);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Clean(instance, windowGlow);

        var rotation = Quaternion.Euler(0f, yaw, 0f);
        var lotCenter = new Vector3(lot.center.x, groundY, lot.center.y);
        instance.transform.SetPositionAndRotation(lotCenter - rotation * variant.CenterOffset, rotation);
        return true;
    }

    static void Clean(GameObject building, Material windowGlow)
    {
        // HDRP decal projectors (stains, leaks) can't run in URP.
        var decals = building.GetComponentsInChildren<Transform>(true)
            .Where(t => t != building.transform && t.name.StartsWith("Decal", System.StringComparison.OrdinalIgnoreCase))
            .Select(t => t.gameObject).ToList();
        foreach (var d in decals)
            if (d != null) Object.DestroyImmediate(d);
        foreach (var t in building.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        if (windowGlow == null) return;
        var window = AssetDatabase.LoadAssetAtPath<Material>(WindowMaterial);
        foreach (var r in building.GetComponentsInChildren<Renderer>(true))
        {
            var materials = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] == window) { materials[i] = windowGlow; changed = true; }
            if (changed) r.sharedMaterials = materials;
        }
    }
}
