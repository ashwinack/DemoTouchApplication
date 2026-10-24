using UnityEngine;
using UnityEngine.UI;

namespace DemoTouch
{
    /// <summary>
    /// Sends the touch control panel and the 3D preview to separate monitors. In a build with fewer
    /// monitors than needed (or when <see cref="forceSplitScreen"/> is on) both share one monitor side by side.
    /// </summary>
    public class MultiDisplaySetup : MonoBehaviour
    {
        [SerializeField] Camera panelCamera;
        [SerializeField] Camera previewCamera;
        [SerializeField] CanvasScaler panelScaler;
        [SerializeField, Tooltip("Monitor index for the touch control panel (0 = primary).")]
        int panelDisplay = 0;
        [SerializeField, Tooltip("Monitor index for the 3D preview.")]
        int previewDisplay = 1;
        [SerializeField, Range(0.2f, 0.6f)] float splitPanelWidth = 0.35f;
        [SerializeField, Tooltip("Show both screens on one monitor even when two are connected.")]
        bool forceSplitScreen;

        void Awake()
        {
            int needed = Mathf.Max(panelDisplay, previewDisplay) + 1;
            // The Editor always reports one display; preview there with Game view tabs set to Display 1 and Display 2.
            bool split = forceSplitScreen || (!Application.isEditor && Display.displays.Length < needed);

            if (split)
            {
                panelCamera.targetDisplay = 0;
                previewCamera.targetDisplay = 0;
                panelCamera.rect = new Rect(0f, 0f, splitPanelWidth, 1f);
                previewCamera.rect = new Rect(splitPanelWidth, 0f, 1f - splitPanelWidth, 1f);
                if (panelScaler != null) panelScaler.matchWidthOrHeight = 0f;
                return;
            }

            Activate(panelDisplay);
            Activate(previewDisplay);
            panelCamera.targetDisplay = panelDisplay;
            previewCamera.targetDisplay = previewDisplay;
            panelCamera.rect = previewCamera.rect = new Rect(0f, 0f, 1f, 1f);
        }

        static void Activate(int index)
        {
            // Display 0 is always active; secondary displays must be switched on explicitly.
            if (index > 0 && index < Display.displays.Length && !Display.displays[index].active)
                Display.displays[index].Activate();
        }
    }
}
