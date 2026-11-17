using System;
using DefenseBattle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>소환 버튼 입력 → SummonHold (탭 1회 / 길게 누르면 연속). Button 은 눌림 그림·비활성 표시만 맡는다</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SummonButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        readonly SummonHold m_hold = new SummonHold();
        Button m_button;

        public Game Game { get; set; }
        /// <summary>지금 소환을 막나 (대전 카운트다운·첫 대전 던지기 안내 - ui.ts summonLock · countingDown). 탭 경로는 Button 활성 상태를 보지 않으므로 여기서 막는다</summary>
        public Func<bool> Blocked;
        /// <summary>골드가 모자라 소환 못 함</summary>
        public event Action GoldShort;
        /// <summary>소환됨</summary>
        public event Action<Unit> Summoned;

        void Awake() => m_button = GetComponent<Button>();

        public void OnPointerDown(PointerEventData e)
        {
            if (Game == null || !m_button.interactable || IsBlocked) return;
            m_hold.PointerDown();
        }

        public void OnPointerUp(PointerEventData e) => m_hold.Release();
        public void OnPointerExit(PointerEventData e) => m_hold.Release();

        public void OnPointerClick(PointerEventData e)
        {
            if (Game == null || IsBlocked) return;
            bool ok = m_hold.Click(Game);
            Report(ok ? 1 : 0);
        }

        void Update()
        {
            if (Game == null || !m_hold.Holding) return;
            if (IsBlocked) { m_hold.Release(); return; }
            Report(m_hold.Tick(Time.unscaledDeltaTime, Game));
        }

        bool IsBlocked => Blocked != null && Blocked();

        void Report(int summoned)
        {
            if (summoned > 0 && m_hold.LastSummoned != null) Summoned?.Invoke(m_hold.LastSummoned);
            if (m_hold.GoldShort) GoldShort?.Invoke();
        }

        void OnDisable() => m_hold.Release();
    }
}
