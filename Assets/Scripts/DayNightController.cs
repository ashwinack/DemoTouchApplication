using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DemoTouch
{
    /// <summary>Sun, sky, ambient and fog settings for one end of the day/night blend.</summary>
    [Serializable]
    public class LightingState
    {
        public float sunIntensity = 2f;
        public Color sunColor = Color.white;
        public Vector3 sunEuler = new Vector3(30f, 30f, 0f);
        public float skyExposure = 1.3f;
        public Color skyTint = Color.gray;
        public float atmosphere = 1f;
        public Color ambientSky = Color.gray, ambientEquator = Color.gray, ambientGround = Color.black;
        public Color fogColor = Color.gray;
        public float fogStart = 180f, fogEnd = 1100f;
    }

    /// <summary>Renderers sharing one material whose emission brightens at night (windows, lamps, signage).</summary>
    [Serializable]
    public class EmissiveGroup
    {
        public string name;
        [ColorUsage(false, true)] public Color day = Color.black;
        [ColorUsage(false, true)] public Color night = Color.white;
        public Renderer[] renderers = Array.Empty<Renderer>();
    }

    /// <summary>
    /// Cross-fades the whole scene between day and night: sun/moon, sky, fog, post-processing,
    /// reflections, window and lamp emission, and the night-only point and spot lights.
    /// Emission is driven through MaterialPropertyBlocks and the skybox is cloned at runtime,
    /// so Play mode never modifies material assets.
    /// </summary>
    public class DayNightController : MonoBehaviour
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int SkyExposure = Shader.PropertyToID("_Exposure");
        static readonly int SkyTint = Shader.PropertyToID("_SkyTint");
        static readonly int Atmosphere = Shader.PropertyToID("_AtmosphereThickness");

        [Header("Scene")]
        [SerializeField] Light sun;
        [SerializeField] Volume dayVolume;
        [SerializeField] Volume nightVolume;
        [SerializeField] ReflectionProbe reflectionProbe;
        [SerializeField] Cubemap dayReflection;
        [SerializeField] Cubemap nightReflection;

        [Header("Looks")]
        [SerializeField] LightingState day = new LightingState();
        [SerializeField] LightingState night = new LightingState();
        [SerializeField] EmissiveGroup[] emissives = Array.Empty<EmissiveGroup>();
        [SerializeField] Light[] nightLights = Array.Empty<Light>();
        [SerializeField] float[] nightLightIntensity = Array.Empty<float>();

        [Header("Transition")]
        [SerializeField, Min(0.1f)] float transitionDuration = 3f;
        [SerializeField] bool startAtNight;

        /// <summary>Raised with the new mode as soon as a switch starts.</summary>
        public event Action<bool> ModeChanged;

        public bool IsNight { get; private set; }

        float blend;
        bool initialized;
        Material runtimeSky;
        MaterialPropertyBlock block;

        Material Sky => runtimeSky != null ? runtimeSky : RenderSettings.skybox;

        void OnEnable()
        {
            if (!initialized)
            {
                initialized = true;
                // Work on a copy so Play mode doesn't overwrite the skybox asset.
                if (RenderSettings.skybox != null)
                    RenderSettings.skybox = runtimeSky = new Material(RenderSettings.skybox);
                IsNight = startAtNight;
                blend = IsNight ? 1f : 0f;
            }
            Apply(blend);
        }

        public void SetNight(bool on)
        {
            if (on == IsNight) return;
            IsNight = on;
            ModeChanged?.Invoke(on);
        }

        public void Toggle() => SetNight(!IsNight);

        void Update()
        {
            float target = IsNight ? 1f : 0f;
            if (Mathf.Approximately(blend, target)) return;
            blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / transitionDuration);
            Apply(blend);
        }

        /// <summary>Applies the look at <paramref name="t"/> (0 = day, 1 = night). Also used by the editor to bake reflections.</summary>
        public void Apply(float t)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            // Artificial lights come on during the second half of dusk and fade out first at dawn.
            float lightsOn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, t));

            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(day.sunIntensity, night.sunIntensity, s);
                sun.color = Color.Lerp(day.sunColor, night.sunColor, s);
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(day.sunEuler), Quaternion.Euler(night.sunEuler), s);
            }

            var sky = Sky;
            if (sky != null)
            {
                sky.SetFloat(SkyExposure, Mathf.Lerp(day.skyExposure, night.skyExposure, s));
                sky.SetColor(SkyTint, Color.Lerp(day.skyTint, night.skyTint, s));
                sky.SetFloat(Atmosphere, Mathf.Lerp(day.atmosphere, night.atmosphere, s));
            }

            RenderSettings.ambientSkyColor = Color.Lerp(day.ambientSky, night.ambientSky, s);
            RenderSettings.ambientEquatorColor = Color.Lerp(day.ambientEquator, night.ambientEquator, s);
            RenderSettings.ambientGroundColor = Color.Lerp(day.ambientGround, night.ambientGround, s);
            RenderSettings.fogColor = Color.Lerp(day.fogColor, night.fogColor, s);
            RenderSettings.fogStartDistance = Mathf.Lerp(day.fogStart, night.fogStart, s);
            RenderSettings.fogEndDistance = Mathf.Lerp(day.fogEnd, night.fogEnd, s);

            if (dayVolume != null) dayVolume.weight = 1f - s;
            if (nightVolume != null) nightVolume.weight = s;

            var cubemap = s < 0.5f ? dayReflection : nightReflection;
            if (reflectionProbe != null && cubemap != null && reflectionProbe.customBakedTexture != cubemap)
            {
                reflectionProbe.customBakedTexture = cubemap;
                RenderSettings.customReflectionTexture = cubemap;
            }

            block ??= new MaterialPropertyBlock();
            foreach (var group in emissives)
            {
                var color = Color.Lerp(group.day, group.night, lightsOn);
                foreach (var r in group.renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(EmissionColor, color);
                    r.SetPropertyBlock(block);
                }
            }

            for (int i = 0; i < nightLights.Length; i++)
            {
                var light = nightLights[i];
                if (light == null) continue;
                light.intensity = nightLightIntensity[i] * lightsOn;
                light.enabled = lightsOn > 0.001f;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring used by the scene generator.</summary>
        public void Configure(Light sunLight, Volume dayLook, Volume nightLook, ReflectionProbe probe,
            LightingState dayState, LightingState nightState, EmissiveGroup[] emissiveGroups, Light[] lights)
        {
            sun = sunLight;
            dayVolume = dayLook;
            nightVolume = nightLook;
            reflectionProbe = probe;
            day = dayState;
            night = nightState;
            emissives = emissiveGroups;
            nightLights = lights;
            nightLightIntensity = Array.ConvertAll(lights, l => l.intensity);
        }

        public void SetReflections(Cubemap dayCubemap, Cubemap nightCubemap)
        {
            dayReflection = dayCubemap;
            nightReflection = nightCubemap;
        }
#endif
    }
}
