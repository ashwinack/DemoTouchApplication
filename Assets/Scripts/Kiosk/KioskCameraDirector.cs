using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace DemoTouch
{
    [Serializable]
    public class ApartmentRig
    {
        public string label = "1 BHK";
        [TextArea] public string details;
        [Tooltip("Shot just outside the apartment's windows; the camera passes through it on the way in and out.")]
        public CinemachineCamera entry;
        [Tooltip("Eye-level shot inside the apartment. Its rotation is the starting view for look-around.")]
        public CinemachineCamera interior;
        [Tooltip("Interior lights, switched on only while this apartment is being shown.")]
        public GameObject lights;
    }

    [Serializable]
    public class BuildingRig
    {
        public string name;
        public Color accent = Color.white;
        public CinemachineCamera exterior;
        public ApartmentRig[] apartments = Array.Empty<ApartmentRig>();
    }

    /// <summary>
    /// Drives the preview camera with Cinemachine. Each preset is a CinemachineCamera; going somewhere means
    /// raising one camera's priority and letting the brain blend with an ease-in-out curve. Moves into or out
    /// of an apartment are routed through its entry shot so the camera flies in through the windows.
    /// </summary>
    public class KioskCameraDirector : MonoBehaviour
    {
        const int LivePriority = 20, StandbyPriority = 0;

        [SerializeField] CinemachineBrain brain;
        [SerializeField] InteriorLook interiorLook;
        [SerializeField] CinemachineCamera idleCamera;
        [SerializeField] BuildingRig[] buildings = Array.Empty<BuildingRig>();

        [Header("Blend times (seconds, ease in-out)")]
        [SerializeField, Min(0.1f)] float toIdle = 3f;
        [SerializeField, Min(0.1f)] float toBuilding = 2.6f;
        [SerializeField, Min(0.1f)] float toEntry = 2.2f;
        [SerializeField, Min(0.1f)] float intoInterior = 1.6f;
        [SerializeField, Min(0.1f)] float outOfInterior = 1.2f;

        public int BuildingCount => buildings.Length;
        public int ApartmentCount(int building) => building >= 0 && building < buildings.Length ? buildings[building].apartments.Length : 0;
        public BuildingRig GetBuilding(int building) => buildings[building];
        public ApartmentRig GetApartment(int building, int apartment) => buildings[building].apartments[apartment];
        public bool IsTransitioning { get; private set; }

        CinemachineCamera live;
        ApartmentRig currentApartment;
        Coroutine route;

        void Awake()
        {
            foreach (var cam in AllCameras()) cam.Priority = StandbyPriority;
            foreach (var building in buildings)
                foreach (var apartment in building.apartments)
                    if (apartment.lights != null) apartment.lights.SetActive(false);
        }

        public void GoIdle(bool instant = false)
        {
            var hops = ExitApartment();
            hops.Add((idleCamera, toIdle));
            Run(hops, instant);
        }

        public void GoBuilding(int building)
        {
            var hops = ExitApartment();
            hops.Add((buildings[building].exterior, toBuilding));
            Run(hops);
        }

        public void GoApartment(int building, int apartment)
        {
            var target = buildings[building].apartments[apartment];
            var hops = ExitApartment();
            hops.Add((target.entry, toEntry));
            hops.Add((target.interior, intoInterior));
            Run(hops, enter: target);
        }

        /// <summary>If the camera is inside an apartment, first back out through its windows.</summary>
        List<(CinemachineCamera cam, float time)> ExitApartment()
        {
            var hops = new List<(CinemachineCamera, float)>();
            if (currentApartment != null)
            {
                hops.Add((currentApartment.entry, outOfInterior));
                interiorLook.Unbind();
            }
            return hops;
        }

        void Run(List<(CinemachineCamera cam, float time)> hops, bool instant = false, ApartmentRig enter = null)
        {
            if (route != null) StopCoroutine(route);
            // Record the destination now, so an interruption mid-flight still exits through the right windows.
            var leaving = currentApartment;
            currentApartment = enter;
            SetLights(enter, leaving);
            route = StartCoroutine(Route(hops, instant, enter));
        }

        /// <summary>Only the apartments the camera is entering or leaving have their lights on.</summary>
        void SetLights(ApartmentRig a, ApartmentRig b)
        {
            foreach (var building in buildings)
                foreach (var apartment in building.apartments)
                    if (apartment.lights != null)
                        apartment.lights.SetActive(apartment == a || apartment == b);
        }

        IEnumerator Route(List<(CinemachineCamera cam, float time)> hops, bool instant, ApartmentRig enter)
        {
            IsTransitioning = true;

            foreach (var (cam, time) in hops)
            {
                brain.DefaultBlend = instant
                    ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                    : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, time);
                if (enter != null && cam == enter.interior)
                    interiorLook.Bind(cam.transform);
                Activate(cam);

                yield return null; // the brain starts the blend on its next update
                while (brain.IsBlending) yield return null;
            }

            SetLights(enter, null);
            IsTransitioning = false;
            route = null;
        }

        void Activate(CinemachineCamera cam)
        {
            if (live != null && live != cam) live.Priority = StandbyPriority;
            cam.Priority = LivePriority;
            live = cam;
        }

        IEnumerable<CinemachineCamera> AllCameras()
        {
            if (idleCamera != null) yield return idleCamera;
            foreach (var building in buildings)
            {
                if (building.exterior != null) yield return building.exterior;
                foreach (var apartment in building.apartments)
                {
                    if (apartment.entry != null) yield return apartment.entry;
                    if (apartment.interior != null) yield return apartment.interior;
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring used by the scene generator.</summary>
        public void Configure(CinemachineBrain cameraBrain, InteriorLook look, CinemachineCamera idle, BuildingRig[] rigs)
        {
            brain = cameraBrain;
            interiorLook = look;
            idleCamera = idle;
            buildings = rigs;
        }
#endif
    }
}
