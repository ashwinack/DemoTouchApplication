using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Converts HDRP/Lit materials (e.g. from Asset Store packs made for HDRP) to URP/Lit in place, so they
/// render in this URP project instead of pink. HDRP properties are read from the serialized material,
/// which still holds them even though the HDRP shader isn't installed. Originals are backed up first.
/// </summary>
public static class HdrpToUrpMaterialConverter
{
    const string BackupRoot = "HDRP Material Backup"; // project root, outside Assets so Unity doesn't import it

    [MenuItem("Tools/Touch Demo/Convert Selected Folder's HDRP Materials to URP")]
    static void ConvertSelectedFolder()
    {
        string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("Convert HDRP materials", "Select a folder in the Project window first.", "OK");
            return;
        }
        int count = ConvertFolder(folder);
        EditorUtility.DisplayDialog("Convert HDRP materials", $"Converted {count} material(s) in {folder}.", "OK");
    }

    /// <summary>Converts every material in <paramref name="folder"/> that isn't already using a working URP shader.</summary>
    public static int ConvertFolder(string folder)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader.name.StartsWith("Universal Render Pipeline")) continue;

            Backup(path);
            var hdrp = ReadSavedProperties(material);
            if (hdrp.Floats.ContainsKey("_DecalBlend") || material.shader.name.Contains("Decal"))
                continue; // decals need URP's decal renderer feature; they are left alone

            material.shader = lit;
            Apply(material, hdrp);
            EditorUtility.SetDirty(material);
            converted++;
        }
        AssetDatabase.SaveAssets();
        return converted;
    }

    static void Backup(string assetPath)
    {
        string target = Path.Combine(BackupRoot, assetPath);
        if (File.Exists(target)) return; // keep the first, original backup
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(assetPath, target);
    }

    class SavedProperties
    {
        public readonly Dictionary<string, (Texture texture, Vector2 scale, Vector2 offset)> Textures = new();
        public readonly Dictionary<string, float> Floats = new();
        public readonly Dictionary<string, Color> Colors = new();
    }

    static SavedProperties ReadSavedProperties(Material material)
    {
        var result = new SavedProperties();
        var saved = new SerializedObject(material).FindProperty("m_SavedProperties");

        var textures = saved.FindPropertyRelative("m_TexEnvs");
        for (int i = 0; i < textures.arraySize; i++)
        {
            var entry = textures.GetArrayElementAtIndex(i);
            var value = entry.FindPropertyRelative("second");
            result.Textures[entry.FindPropertyRelative("first").stringValue] = (
                value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture,
                value.FindPropertyRelative("m_Scale").vector2Value,
                value.FindPropertyRelative("m_Offset").vector2Value);
        }

        var floats = saved.FindPropertyRelative("m_Floats");
        for (int i = 0; i < floats.arraySize; i++)
        {
            var entry = floats.GetArrayElementAtIndex(i);
            result.Floats[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second").floatValue;
        }

        var colors = saved.FindPropertyRelative("m_Colors");
        for (int i = 0; i < colors.arraySize; i++)
        {
            var entry = colors.GetArrayElementAtIndex(i);
            result.Colors[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second").colorValue;
        }
        return result;
    }

    static void Apply(Material m, SavedProperties hdrp)
    {
        float F(string name, float fallback) => hdrp.Floats.TryGetValue(name, out var v) ? v : fallback;
        Texture T(string name) => hdrp.Textures.TryGetValue(name, out var t) ? t.texture : null;

        // Base colour and tiling.
        if (hdrp.Colors.TryGetValue("_BaseColor", out var baseColor)) m.SetColor("_BaseColor", baseColor);
        if (hdrp.Textures.TryGetValue("_BaseColorMap", out var baseMap) && baseMap.texture != null)
        {
            m.SetTexture("_BaseMap", baseMap.texture);
            m.SetTextureScale("_BaseMap", baseMap.scale);
            m.SetTextureOffset("_BaseMap", baseMap.offset);
        }

        // Normal map.
        if (T("_NormalMap") is Texture normal)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", F("_NormalScale", 1f));
            m.EnableKeyword("_NORMALMAP");
        }

        // HDRP mask map: R metallic, G occlusion, B detail mask, A smoothness. URP's metallic map reads
        // R metallic + A smoothness and its occlusion map reads G, so the same texture serves both.
        if (T("_MaskMap") is Texture mask)
        {
            m.SetTexture("_MetallicGlossMap", mask);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", F("_SmoothnessRemapMax", 1f));
            m.SetTexture("_OcclusionMap", mask);
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_OcclusionStrength", 1f);
        }
        else
        {
            m.SetFloat("_Metallic", F("_Metallic", 0f));
            m.SetFloat("_Smoothness", F("_Smoothness", 0.5f));
        }

        // Emission.
        bool emissive = false;
        if (T("_EmissiveColorMap") is Texture emissionMap)
        {
            m.SetTexture("_EmissionMap", emissionMap);
            emissive = true;
        }
        if (hdrp.Colors.TryGetValue("_EmissiveColor", out var emission) && emission.maxColorComponent > 0.001f)
        {
            // HDRP emission is in physical units; scale it down to URP's relative range.
            float intensity = Mathf.Max(emission.r, emission.g, emission.b);
            m.SetColor("_EmissionColor", intensity > 8f ? emission / intensity * 4f : emission);
            emissive = true;
        }
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        // Alpha clipping (leaves, foliage cards).
        if (F("_AlphaCutoffEnable", 0f) > 0.5f)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", F("_AlphaCutoff", 0.5f));
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }

        // Transparent surfaces (glass).
        if (F("_SurfaceType", 0f) > 0.5f)
        {
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
        }

        // Double-sided geometry (leaves, thin panels).
        if (F("_DoubleSidedEnable", 0f) > 0.5f) m.SetFloat("_Cull", (float)CullMode.Off);
    }
}
