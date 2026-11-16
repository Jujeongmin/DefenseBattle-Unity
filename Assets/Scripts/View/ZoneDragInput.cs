using UnityEngine;
using UnityEngine.EventSystems;

namespace DefenseBattle.View
{
    /// <summary>전장 칸 위 투명 입력면: 화면 좌표 → 논리 좌표 → ZoneDragLogic. 그리기는 BoardView 가 Logic 을 읽어 한다</summary>
    public sealed class ZoneDragInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] BattleRunner m_runner;

        public readonly ZoneDragLogic Logic = new ZoneDragLogic();
        /// <summary>패널·결과 창이 열려 있으면 true (드래그를 받지 않는다)</summary>
        public bool Blocked { get; set; }

        Vector2 ToLogical(Vector2 screen)
        {
            Vector3 w = m_runner.Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            return BoardSpace.ToLogical(w);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (m_runner.Game == null) return;
            Logic.Down(e.pointerId, ToLogical(e.position), m_runner.Game, Blocked);
        }

        public void OnDrag(PointerEventData e) => Logic.Move(e.pointerId, ToLogical(e.position));

        public void OnPointerUp(PointerEventData e)
        {
            if (m_runner.Game == null) return;
            Logic.Up(e.pointerId, ToLogical(e.position), m_runner.Game, Blocked);
        }

        void Update()
        {
            if (Logic.Active && (Blocked || m_runner.Game == null || m_runner.Game.Over)) Logic.Cancel();
        }
    }
}
