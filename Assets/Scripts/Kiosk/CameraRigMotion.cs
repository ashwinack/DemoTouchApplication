using UnityEngine;

namespace DemoTouch
{
    /// <summary>
    /// Slowly turns a pivot that a preset camera is parented to: a continuous orbit for the idle shot,
    /// or a gentle side-to-side sway so building shots never look frozen.
    /// </summary>
    public class CameraRigMotion : MonoBehaviour
    {
        public enum Mode { Orbit, Sway }

        [SerializeField] Mode mode = Mode.Orbit;
        [SerializeField, Tooltip("Orbit: degrees per second. Sway: maximum angle either side.")]
        float degrees = 3f;
        [SerializeField, Tooltip("Sway cycles per minute.")] float swayRate = 2.5f;

        Quaternion start;

        void Awake() => start = transform.localRotation;

        void Update()
        {
            float angle = mode == Mode.Orbit
                ? Time.unscaledTime * degrees
                : Mathf.Sin(Time.unscaledTime * swayRate / 60f * Mathf.PI * 2f) * degrees;
            transform.localRotation = start * Quaternion.Euler(0f, angle, 0f);
        }

#if UNITY_EDITOR
        public void Configure(Mode motion, float angle, float cyclesPerMinute = 2.5f)
        {
            mode = motion;
            degrees = angle;
            swayRate = cyclesPerMinute;
        }
#endif
    }
}
