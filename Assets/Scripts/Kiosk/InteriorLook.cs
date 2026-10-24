using System.Collections.Generic;
using UnityEngine;

namespace DemoTouch
{
    /// <summary>
    /// 360° look-around for the active interior camera. Drag input sets a target yaw/pitch; the camera
    /// follows with smooth damping, and a flick keeps it turning briefly with inertia. Pitch is clamped.
    /// </summary>
    [DefaultExecutionOrder(-100)] // rotate the camera before the Cinemachine brain reads it
    public class InteriorLook : MonoBehaviour
    {
        [SerializeField, Tooltip("Degrees turned by dragging across the full width of the preview.")]
        float degreesPerPreviewWidth = 200f;
        [SerializeField] Vector2 pitchLimits = new Vector2(-35f, 50f);
        [SerializeField, Min(0.01f)] float smoothTime = 0.12f;
        [SerializeField, Min(0f), Tooltip("How quickly a flick's spin dies away after release.")]
        float inertiaDamping = 4f;

        readonly Dictionary<Transform, Vector2> homeRotations = new Dictionary<Transform, Vector2>();
        Transform target;
        float yaw, pitch, targetYaw, targetPitch, yawVelocity, pitchVelocity;
        Vector2 flick;
        bool dragging;

        public bool IsBound => target != null;

        /// <summary>Starts controlling <paramref name="cam"/>, resetting to the rotation it was authored with.</summary>
        public void Bind(Transform cam)
        {
            if (!homeRotations.TryGetValue(cam, out var home))
            {
                var euler = cam.rotation.eulerAngles;
                homeRotations[cam] = home = new Vector2(euler.y, Mathf.DeltaAngle(0f, euler.x));
            }
            if (target != null && target != cam) ResetToHome(target);

            target = cam;
            yaw = targetYaw = home.x;
            pitch = targetPitch = Mathf.Clamp(home.y, pitchLimits.x, pitchLimits.y);
            yawVelocity = pitchVelocity = 0f;
            flick = Vector2.zero;
            dragging = false;
            Apply();
        }

        public void Unbind()
        {
            if (target != null) ResetToHome(target);
            target = null;
        }

        public void BeginDrag()
        {
            dragging = true;
            flick = Vector2.zero;
        }

        /// <summary>Drag movement as a fraction of the preview width (x and y both normalised by width).</summary>
        public void Drag(Vector2 delta)
        {
            if (target == null) return;
            // "Grab the world": dragging right turns the view left, dragging up tilts it down.
            var turn = new Vector2(-delta.x, delta.y) * degreesPerPreviewWidth;
            targetYaw += turn.x;
            targetPitch = Mathf.Clamp(targetPitch + turn.y, pitchLimits.x, pitchLimits.y);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            flick = Vector2.Lerp(flick, turn / dt, 0.5f);
        }

        public void EndDrag() => dragging = false;

        void Update()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;

            if (!dragging && flick.sqrMagnitude > 0.01f)
            {
                targetYaw += flick.x * dt;
                targetPitch = Mathf.Clamp(targetPitch + flick.y * dt, pitchLimits.x, pitchLimits.y);
                flick *= Mathf.Exp(-inertiaDamping * dt);
            }

            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, smoothTime, Mathf.Infinity, dt);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, smoothTime, Mathf.Infinity, dt);
            Apply();
        }

        void Apply() => target.rotation = Quaternion.Euler(pitch, yaw, 0f);

        void ResetToHome(Transform cam)
        {
            if (homeRotations.TryGetValue(cam, out var home))
                cam.rotation = Quaternion.Euler(home.y, home.x, 0f);
        }
    }
}
