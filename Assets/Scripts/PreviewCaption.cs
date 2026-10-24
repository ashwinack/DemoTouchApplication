using UnityEngine;
using UnityEngine.UI;

namespace DemoTouch
{
    /// <summary>Caption on Display 2 that cross-fades to describe what the camera is showing.</summary>
    public class PreviewCaption : MonoBehaviour
    {
        [SerializeField] KioskStateManager state;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text title;
        [SerializeField] Text subtitle;
        [SerializeField, Min(0.01f)] float fadeDuration = 0.35f;

        string shown;

        void Awake() => group.alpha = 0f;

        void Update()
        {
            Describe(out string wantedTitle, out string wantedSubtitle);
            float step = Time.unscaledDeltaTime / fadeDuration;

            if (wantedTitle + wantedSubtitle == shown)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 1f, step);
                return;
            }

            group.alpha = Mathf.MoveTowards(group.alpha, 0f, step);
            if (group.alpha > 0f) return;

            shown = wantedTitle + wantedSubtitle;
            title.text = wantedTitle;
            subtitle.text = wantedSubtitle;
        }

        void Describe(out string heading, out string detail)
        {
            var director = state.Director;
            switch (state.State)
            {
                case KioskState.BuildingView:
                    heading = director.GetBuilding(state.Building).name;
                    detail = $"BUILDING {state.Building + 1:00}  ·  CHOOSE AN APARTMENT";
                    break;
                case KioskState.ApartmentView:
                    var apartment = director.GetApartment(state.Building, state.Apartment);
                    heading = $"{director.GetBuilding(state.Building).name}  ·  {apartment.label}";
                    detail = "SWIPE THE PANEL PREVIEW TO LOOK AROUND";
                    break;
                default:
                    heading = "City Overview";
                    detail = "TAP A BUILDING ON THE CONTROL PANEL";
                    break;
            }
        }
    }
}
