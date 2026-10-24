using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds and caches meshes whose UVs are measured in metres, so tiled facade textures keep the same
/// window size on every face regardless of the object's dimensions. All meshes are stored in one asset.
/// </summary>
static class MeshLibrary
{
    const string AssetPath = "Assets/Generated/CityMeshes.asset";

    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
    static Mesh container;

    public static void Begin()
    {
        cache.Clear();
        AssetDatabase.DeleteAsset(AssetPath);
        container = new Mesh { name = "City Meshes" };
        AssetDatabase.CreateAsset(container, AssetPath);
    }

    public static void Save(Mesh mesh) => AssetDatabase.AddObjectToAsset(mesh, container);

    /// <param name="uvOffset">Shifts the texture in metres, so stacked boxes (e.g. one per floor) don't all start on the same window row.</param>
    public static Mesh Box(Vector3 size, Vector2 uvOffset = default)
    {
        string key = $"Box {size.x:0.##} x {size.y:0.##} x {size.z:0.##}";
        if (uvOffset != Vector2.zero) key += $" uv {uvOffset.x:0.##},{uvOffset.y:0.##}";
        if (cache.TryGetValue(key, out var mesh)) return mesh;

        var b = new Builder { UvOffset = uvOffset };
        float x = size.x / 2f, y = size.y / 2f, z = size.z / 2f;
        b.Quad(new Vector3(-x, -y, -z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.back);
        b.Quad(new Vector3(x, -y, z), new Vector3(-size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.forward);
        b.Quad(new Vector3(-x, -y, z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0), Vector3.left);
        b.Quad(new Vector3(x, -y, -z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), Vector3.right);
        b.Quad(new Vector3(-x, y, -z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), Vector3.up);
        b.Quad(new Vector3(-x, -y, z), new Vector3(size.x, 0, 0), new Vector3(0, 0, -size.z), Vector3.down);
        return Store(key, b);
    }

    /// <summary>Vertical cylinder centred on the origin; side UVs run along the circumference in metres.</summary>
    public static Mesh Cylinder(float radius, float height, int segments = 32)
    {
        string key = $"Cylinder r{radius:0.##} h{height:0.##} s{segments}";
        if (cache.TryGetValue(key, out var mesh)) return mesh;

        var b = new Builder();
        float h = height / 2f;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            var n0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var n1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            int v0 = b.Vertex(n0 * radius + Vector3.down * h, n0, new Vector2(a0 * radius, 0f));
            int v1 = b.Vertex(n0 * radius + Vector3.up * h, n0, new Vector2(a0 * radius, height));
            int v2 = b.Vertex(n1 * radius + Vector3.up * h, n1, new Vector2(a1 * radius, height));
            int v3 = b.Vertex(n1 * radius + Vector3.down * h, n1, new Vector2(a1 * radius, 0f));
            var mid = (n0 + n1).normalized;
            b.Triangle(v0, v1, v2, mid);
            b.Triangle(v0, v2, v3, mid);

            foreach (float side in new[] { 1f, -1f })
            {
                var n = Vector3.up * side;
                var p0 = n0 * radius + n * h;
                var p1 = n1 * radius + n * h;
                int c = b.Vertex(n * h, n, Vector2.zero);
                int r0 = b.Vertex(p0, n, new Vector2(p0.x, p0.z));
                int r1 = b.Vertex(p1, n, new Vector2(p1.x, p1.z));
                b.Triangle(c, r0, r1, n);
            }
        }
        return Store(key, b);
    }

    /// <summary>Low-poly UV sphere of radius 0.5, used for tree crowns and beacon lights.</summary>
    public static Mesh Sphere()
    {
        const string key = "Sphere";
        if (cache.TryGetValue(key, out var mesh)) return mesh;

        const int lon = 12, lat = 8;
        var b = new Builder();
        Vector3 Point(int i, int j)
        {
            float theta = j / (float)lat * Mathf.PI, phi = i / (float)lon * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
        }
        for (int j = 0; j < lat; j++)
        for (int i = 0; i < lon; i++)
        {
            var p0 = Point(i, j); var p1 = Point(i + 1, j); var p2 = Point(i + 1, j + 1); var p3 = Point(i, j + 1);
            int a = b.Vertex(p0, p0.normalized, new Vector2(i, j));
            int c = b.Vertex(p1, p1.normalized, new Vector2(i + 1, j));
            int d = b.Vertex(p2, p2.normalized, new Vector2(i + 1, j + 1));
            int e = b.Vertex(p3, p3.normalized, new Vector2(i, j + 1));
            var n = (p0 + p1 + p2 + p3).normalized;
            b.Triangle(a, c, d, n);
            b.Triangle(a, d, e, n);
        }
        return Store(key, b);
    }

    static Mesh Store(string key, Builder builder)
    {
        var mesh = builder.ToMesh(key);
        Save(mesh);
        cache[key] = mesh;
        return mesh;
    }

    class Builder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        public Vector2 UvOffset;

        public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv + UvOffset);
            return vertices.Count - 1;
        }

        /// <summary>Adds a triangle wound so it faces <paramref name="facing"/>.</summary>
        public void Triangle(int a, int b, int c, Vector3 facing)
        {
            var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(cross, facing) < 0f) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>Quad from origin <paramref name="o"/> spanning u (horizontal) and v (vertical); UVs in metres.</summary>
        public void Quad(Vector3 o, Vector3 u, Vector3 v, Vector3 normal)
        {
            float lu = u.magnitude, lv = v.magnitude;
            int a = Vertex(o, normal, new Vector2(0f, 0f));
            int b = Vertex(o + u, normal, new Vector2(lu, 0f));
            int c = Vertex(o + u + v, normal, new Vector2(lu, lv));
            int d = Vertex(o + v, normal, new Vector2(0f, lv));
            Triangle(a, b, c, normal);
            Triangle(a, c, d, normal);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

/// <summary>Collects many small parts and merges them into one mesh per material to keep draw calls low.</summary>
class MeshBatch
{
    readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();

    public void Add(Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, int subMesh = 0)
    {
        if (!parts.TryGetValue(material, out var list)) parts[material] = list = new List<CombineInstance>();
        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = subMesh, transform = Matrix4x4.TRS(position, rotation, scale) });
    }

    public void Box(Vector3 center, Vector3 size, Material material, float yaw = 0f) =>
        Add(MeshLibrary.Box(size), material, center, Quaternion.Euler(0f, yaw, 0f), Vector3.one);

    public void Flush(string name, Transform parent)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        foreach (var pair in parts)
        {
            var mesh = new Mesh { name = $"{name} - {pair.Key.name}", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            MeshLibrary.Save(mesh);

            var go = new GameObject(pair.Key.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
        }
        parts.Clear();
    }
}
