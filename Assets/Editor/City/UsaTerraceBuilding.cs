using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assembles the "USA Building With Terrace (HDRP, PBR)" asset from its modular prefabs (podium, terrace,
/// ten tower sections and roof) using the layout of the pack's sample scene, then strips the HDRP-only
/// parts that can't run in URP (decal projectors and their missing scripts).
/// </summary>
static class UsaTerraceBuilding
{
    const string Prefabs = "Assets/USA Building With Terrace (HDRP, PBR)/Prefabs/";

    // Module positions relative to the podium, taken from the pack's sample scene.
    static readonly (string prefab, Vector3 offset)[] Layout =
    {
        ("_bottom", new Vector3(0f, 0f, 0f)),
        ("_middle", new Vector3(0f, 8.51f, 0f)),
        ("_terrace", new Vector3(37.8f, 17.51f, -36f)),
        ("_top", new Vector3(37.8f, 25.57f, -36f)),
        ("_top", new Vector3(37.8f, 41.70f, -36f)),
        ("_top", new Vector3(37.8f, 57.82f, -36f)),
        ("_top", new Vector3(37.8f, 73.95f, -36f)),
        ("_top", new Vector3(37.8f, 90.07f, -36f)),
        ("_top", new Vector3(37.8f, 106.20f, -36f)),
        ("_top", new Vector3(37.8f, 122.32f, -36f)),
        ("_top", new Vector3(37.8f, 138.45f, -36f)),
        ("_top", new Vector3(37.8f, 154.57f, -36f)),
        ("_top", new Vector3(37.8f, 170.70f, -36f)),
        ("_roof", new Vector3(43.77f, 187.04f, -44.14f)),
    };

    public static bool IsAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "_bottom.prefab") != null;

    public class Result
    {
        public Bounds LocalBounds;          // in the root's space
        public GameObject[] Modules;        // in Layout order: 0 bottom, 1 middle, 2 terrace, 3..12 tower sections, 13 roof
        public GameObject Podium => Modules[1];
        public GameObject TowerSection(int i) => Modules[3 + i];
    }

    /// <summary>
    /// Builds the model under <paramref name="root"/>, centred on its footprint, standing on the root's origin
    /// and turned by <paramref name="yaw"/> degrees.
    /// </summary>
    public static Result Build(Transform root, float yaw)
    {
        var model = new GameObject("USA Building With Terrace").transform;
        model.SetParent(root, false);

        var modules = new GameObject[Layout.Length];
        for (int i = 0; i < Layout.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + Layout[i].prefab + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, model);
            instance.transform.localPosition = Layout[i].offset;
            // Unpacked so the HDRP-only children can be removed from this copy (the pack's prefabs stay untouched).
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            modules[i] = instance;
        }

        // HDRP decals (stains, leaks) need HDRP's DecalProjector, which doesn't exist in URP.
        var remove = new List<GameObject>();
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("decal", System.StringComparison.OrdinalIgnoreCase)) remove.Add(t.gameObject);
        foreach (var go in remove)
            if (go != null) Object.DestroyImmediate(go);
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        // Centre the footprint on the root, stand it on the ground, then turn it about its centre.
        var bounds = WorldBounds(model.gameObject);
        model.position += root.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.RotateAround(root.position, Vector3.up, yaw);

        bounds = WorldBounds(model.gameObject);
        return new Result { LocalBounds = new Bounds(root.InverseTransformPoint(bounds.center), bounds.size), Modules = modules };
    }

    /// <summary>
    /// Distance along the root's -z axis to the building's south facade at (x, y) in root space, found by
    /// ray-casting against temporary colliders on the model.
    /// </summary>
    public static float SouthFaceZ(Transform root, GameObject[] modules, float x, float y)
    {
        var colliders = new List<MeshCollider>();
        foreach (var module in modules)
            foreach (var filter in module.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null)
                    colliders.Add(filter.gameObject.AddComponent<MeshCollider>());
        Physics.SyncTransforms();

        var origin = root.TransformPoint(new Vector3(x, y, -300f));
        float z = float.NaN;
        if (Physics.Raycast(origin, root.forward, out var hit, 600f))
            z = root.InverseTransformPoint(hit.point).z;

        foreach (var c in colliders) Object.DestroyImmediate(c);
        return z;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
