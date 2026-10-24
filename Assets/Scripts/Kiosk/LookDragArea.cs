using UnityEngine;
using UnityEngine.EventSystems;

namespace DemoTouch
{
    /// <summary>Turns swipes/drags on a UI element (the live preview on Display 1) into look-around input.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class LookDragArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] InteriorLook look;
        [SerializeField] KioskStateManager state;

        RectTransform rect;
        Canvas canvas;

        void Awake()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>().rootCanvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            look.BeginDrag();
            state.RegisterInteraction();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Pointer deltas are in screen pixels; the rect is in canvas units.
            float widthPixels = rect.rect.width * canvas.scaleFactor;
            look.Drag(eventData.delta / Mathf.Max(widthPixels, 1f));
            state.RegisterInteraction();
        }

        public void OnEndDrag(PointerEventData eventData) => look.EndDrag();
    }
}
