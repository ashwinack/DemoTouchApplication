using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemoTouch
{
    /// <summary>
    /// Display 1 touch panel. Shows one page per kiosk state (building list, apartment choice, live
    /// look-around preview), cross-fading between them, and forwards taps to the state manager.
    /// </summary>
    public class KioskPanelUI : MonoBehaviour
    {
        [SerializeField] KioskStateManager state;
        [SerializeField] DayNightController dayNight;
        [SerializeField, Min(0.01f)] float pageFade = 0.25f;

        [Header("Pages")]
        [SerializeField] CanvasGroup idlePage;
        [SerializeField] CanvasGroup buildingPage;
        [SerializeField] CanvasGroup apartmentPage;
        [SerializeField] Text breadcrumb;

        [Header("Idle page")]
        [SerializeField] Button[] buildingButtons;

        [Header("Building page")]
        [SerializeField] Text buildingTitle;
        [SerializeField] Image buildingAccent;
        [SerializeField] Button[] apartmentButtons;
        [SerializeField] Text[] apartmentLabels;
        [SerializeField] Text[] apartmentDetails;
        [SerializeField] Button buildingBack;

        [Header("Apartment page")]
        [SerializeField] Text apartmentTitle;
        [SerializeField] Text apartmentInfo;
        [SerializeField] Button apartmentBack;
        [SerializeField] Button apartmentHome;

        [Header("Day / night")]
        [SerializeField] Button dayNightButton;
        [SerializeField] Text dayNightLabel;
        [SerializeField] Color normalColor = new Color(0.13f, 0.16f, 0.22f);
        [SerializeField] Color nightColor = new Color(0.32f, 0.24f, 0.62f);

        UnityAction[] selectBuilding, selectApartment;

        // Wiring lives in OnEnable/OnDisable (not Awake) so it also works with
        // Enter Play Mode Options that skip the scene reload.
        void OnEnable()
        {
            selectBuilding ??= Actions(buildingButtons.Length, i => () => state.SelectBuilding(i));
            selectApartment ??= Actions(apartmentButtons.Length, i => () => state.SelectApartment(i));

            for (int i = 0; i < buildingButtons.Length; i++) buildingButtons[i].onClick.AddListener(selectBuilding[i]);
            for (int i = 0; i < apartmentButtons.Length; i++) apartmentButtons[i].onClick.AddListener(selectApartment[i]);
            buildingBack.onClick.AddListener(state.Back);
            apartmentBack.onClick.AddListener(state.Back);
            apartmentHome.onClick.AddListener(state.Home);
            state.StateChanged += Refresh;

            if (dayNight != null)
            {
                dayNightButton.onClick.AddListener(dayNight.Toggle);
                dayNight.ModeChanged += RefreshDayNight;
                RefreshDayNight(dayNight.IsNight);
            }
            Refresh();
            SnapPages();
        }

        void OnDisable()
        {
            for (int i = 0; i < buildingButtons.Length; i++) buildingButtons[i].onClick.RemoveListener(selectBuilding[i]);
            for (int i = 0; i < apartmentButtons.Length; i++) apartmentButtons[i].onClick.RemoveListener(selectApartment[i]);
            buildingBack.onClick.RemoveListener(state.Back);
            apartmentBack.onClick.RemoveListener(state.Back);
            apartmentHome.onClick.RemoveListener(state.Home);
            state.StateChanged -= Refresh;

            if (dayNight != null)
            {
                dayNightButton.onClick.RemoveListener(dayNight.Toggle);
                dayNight.ModeChanged -= RefreshDayNight;
            }
        }

        static UnityAction[] Actions(int count, System.Func<int, UnityAction> make)
        {
            var actions = new UnityAction[count];
            for (int i = 0; i < count; i++) actions[i] = make(i);
            return actions;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / pageFade;
            Fade(idlePage, state.State == KioskState.Idle, step);
            Fade(buildingPage, state.State == KioskState.BuildingView, step);
            Fade(apartmentPage, state.State == KioskState.ApartmentView, step);
            HandleKeyboard();
        }

        static void Fade(CanvasGroup page, bool visible, float step)
        {
            page.alpha = Mathf.MoveTowards(page.alpha, visible ? 1f : 0f, step);
            page.interactable = page.blocksRaycasts = visible;
        }

        void SnapPages()
        {
            idlePage.alpha = state.State == KioskState.Idle ? 1f : 0f;
            buildingPage.alpha = state.State == KioskState.BuildingView ? 1f : 0f;
            apartmentPage.alpha = state.State == KioskState.ApartmentView ? 1f : 0f;
        }

        /// <summary>Keyboard shortcuts for testing without a touchscreen.</summary>
        void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            int number = keyboard.digit1Key.wasPressedThisFrame ? 0
                : keyboard.digit2Key.wasPressedThisFrame ? 1
                : keyboard.digit3Key.wasPressedThisFrame ? 2 : -1;
            if (number >= 0)
            {
                if (state.State == KioskState.Idle) state.SelectBuilding(number);
                else state.SelectApartment(number);
            }
            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame) state.Back();
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame) state.Home();
            if (keyboard.nKey.wasPressedThisFrame && dayNight != null) dayNight.Toggle();
        }

        void Refresh()
        {
            var director = state.Director;
            string crumb = "HOME";

            if (state.Building >= 0)
            {
                var building = director.GetBuilding(state.Building);
                crumb += "  ›  " + building.name.ToUpperInvariant();
                buildingTitle.text = building.name.ToUpperInvariant();
                buildingAccent.color = building.accent;

                for (int i = 0; i < apartmentButtons.Length; i++)
                {
                    bool exists = i < building.apartments.Length;
                    apartmentButtons[i].gameObject.SetActive(exists);
                    if (!exists) continue;
                    apartmentLabels[i].text = building.apartments[i].label;
                    apartmentDetails[i].text = building.apartments[i].details;
                }

                if (state.Apartment >= 0)
                {
                    var apartment = building.apartments[state.Apartment];
                    crumb += "  ›  " + apartment.label;
                    apartmentTitle.text = $"{apartment.label}\n<size=30>{building.name.ToUpperInvariant()}</size>";
                    apartmentInfo.text = apartment.details;
                }
            }
            breadcrumb.text = crumb;
        }

        void RefreshDayNight(bool night)
        {
            dayNightButton.targetGraphic.color = night ? nightColor : normalColor;
            if (dayNightLabel != null) dayNightLabel.text = night ? "DAY" : "NIGHT";
        }
    }
}
