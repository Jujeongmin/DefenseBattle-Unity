using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DefenseBattle.View
{
    /// <summary>
    /// 스포트라이트 대상에 잠깐 붙여 탭을 알린다. 같은 오브젝트의 Button·SummonButton 클릭은 그대로 동작한다
    /// (클릭 이벤트는 한 오브젝트의 모든 IPointerClickHandler 에 간다)
    /// </summary>
    public sealed class SpotTap : MonoBehaviour, IPointerClickHandler
    {
        public Action Tapped;

        public void OnPointerClick(PointerEventData e) => Tapped?.Invoke();
    }
}
