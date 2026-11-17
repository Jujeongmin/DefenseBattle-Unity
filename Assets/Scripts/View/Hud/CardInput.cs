using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DefenseBattle.View
{
    /// <summary>몹 카드의 누름·끌기·뗌을 넘겨준다 (ui.ts attack-card pointerdown / move / up)</summary>
    public sealed class CardInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerUpHandler
    {
        public Action<PointerEventData> Down, Drag, Up;

        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e);
        public void OnBeginDrag(PointerEventData e) => Drag?.Invoke(e);
        public void OnDrag(PointerEventData e) => Drag?.Invoke(e);
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(e);
    }
}
