using UnityEngine;

namespace DemoTouch
{
    /// <summary>
    /// Hides parts of a building model (e.g. the floor section an apartment sits in) while the camera is inside
    /// the apartment, so the view out of the windows isn't blocked by the model's facade details. The test box
    /// is in this object's local space: x across the window wall, y up from the floor, z into the apartment.
    /// </summary>
    public class HideWhileCameraInside : MonoBehaviour
    {
        [SerializeField] Renderer[] hide;
        [SerializeField, Tooltip("Apartment size: width (x), height (y), depth (z).")]
        Vector3 size = new Vector3(14f, 3f, 9f);
        [SerializeField, Tooltip("How far outside the window wall (-z) the camera counts as inside, so the facade is hidden just before the camera passes through it.")]
        float facadeMargin = 0.8f;
        [SerializeField, Tooltip("Camera to test; defaults to Camera.main.")]
        Transform viewer;

        bool hidden;

        void LateUpdate()
        {
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            if (viewer == null) return;

            var p = transform.InverseTransformPoint(viewer.position);
            bool inside = Mathf.Abs(p.x) < size.x / 2f + 0.5f
                          && p.y > -0.5f && p.y < size.y + 0.5f
                          && p.z > -facadeMargin && p.z < size.z + 0.5f;
            if (inside == hidden) return;

            hidden = inside;
            foreach (var r in hide)
                if (r != null) r.forceRenderingOff = inside;
        }

        void OnDisable()
        {
            if (!hidden) return;
            hidden = false;
            foreach (var r in hide)
                if (r != null) r.forceRenderingOff = false;
        }

#if UNITY_EDITOR
        public void Configure(Renderer[] renderers, Vector3 apartmentSize, Transform watched)
        {
            hide = renderers;
            size = apartmentSize;
            viewer = watched;
        }
#endif
    }
}
