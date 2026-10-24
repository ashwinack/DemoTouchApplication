using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemoTouch
{
    public enum KioskState
    {
        Idle,
        BuildingView,
        ApartmentView,
    }

    /// <summary>
    /// Central state machine for the kiosk: Idle → BuildingView → ApartmentView. UI calls the public
    /// methods; the camera director carries out the moves. Returns to Idle after a period without input.
    /// </summary>
    public class KioskStateManager : MonoBehaviour
    {
        [SerializeField] KioskCameraDirector director;
        [SerializeField, Min(5f), Tooltip("Seconds without any touch, click or key press before returning to Idle.")]
        float idleTimeout = 60f;

        /// <summary>Raised after every state change (including a change of building or apartment).</summary>
        public event Action StateChanged;

        public KioskState State { get; private set; } = KioskState.Idle;
        public int Building { get; private set; } = -1;
        public int Apartment { get; private set; } = -1;
        public KioskCameraDirector Director => director;
        public float SecondsUntilIdle => Mathf.Max(0f, idleTimeout - (Time.unscaledTime - lastInteraction));

        float lastInteraction;

        void Start()
        {
            lastInteraction = Time.unscaledTime;
            director.GoIdle(instant: true);
            StateChanged?.Invoke();
        }

        void Update()
        {
            if (AnyInput()) lastInteraction = Time.unscaledTime;
            if (State != KioskState.Idle && Time.unscaledTime - lastInteraction > idleTimeout)
                Home();
        }

        public void SelectBuilding(int building)
        {
            RegisterInteraction();
            if (building < 0 || building >= director.BuildingCount) return;
            if (State == KioskState.BuildingView && Building == building) return;

            Building = building;
            Apartment = -1;
            State = KioskState.BuildingView;
            director.GoBuilding(building);
            StateChanged?.Invoke();
        }

        public void SelectApartment(int apartment)
        {
            RegisterInteraction();
            if (State == KioskState.Idle || apartment < 0 || apartment >= director.ApartmentCount(Building)) return;
            if (State == KioskState.ApartmentView && Apartment == apartment) return;

            Apartment = apartment;
            State = KioskState.ApartmentView;
            director.GoApartment(Building, apartment);
            StateChanged?.Invoke();
        }

        /// <summary>One level up: apartment → its building, building → idle.</summary>
        public void Back()
        {
            RegisterInteraction();
            switch (State)
            {
                case KioskState.ApartmentView:
                    int building = Building;
                    Building = -1; // force SelectBuilding to run even though it's the same building
                    State = KioskState.BuildingView;
                    SelectBuilding(building);
                    break;
                case KioskState.BuildingView:
                    Home();
                    break;
            }
        }

        public void Home()
        {
            if (State == KioskState.Idle) return;
            Building = -1;
            Apartment = -1;
            State = KioskState.Idle;
            director.GoIdle();
            StateChanged?.Invoke();
        }

        public void RegisterInteraction() => lastInteraction = Time.unscaledTime;

        static bool AnyInput()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }
    }
}
