using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 상대 전장 미니맵 (index.html #opp-canvas, ui.ts bindOppPeek): 평소엔 VS 줄 오른쪽 작은 칸,
    /// 누르고 있거나(0.15초 뒤) 카드를 조준하는 동안엔 내 전장 위로 크게 (0.15초 동안 커짐).
    /// </summary>
    public sealed class OppPeek : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const float Anim = 0.15f, HoldDelay = 0.15f, BigFrac = 0.96f;

        RectTransform m_rt, m_small, m_board, m_parent;
        RawImage m_image;
        float m_t;          // 0 = 작게, 1 = 크게
        float m_hold = -1;  // 누르고 있는 시간
        float m_shake;      // 던진 몹이 닿아 흔들리는 남은 시간
        bool m_pressed;
        readonly Vector3[] m_corners = new Vector3[4];

        /// <summary>조준 중 강제로 크게</summary>
        public bool Aiming { get; set; }
        public bool Expanded => m_t >= 1;
        /// <summary>지금 보이는 미니맵 화면 칸 (던질 위치 계산용)</summary>
        public RectTransform View => m_rt;

        /// <summary>던진 몹이 닿음 → 잠깐 흔들림 (ui.ts opp-canvas .hit)</summary>
        public void Hit() => m_shake = 0.25f;

        public static OppPeek Create(RectTransform parent, RectTransform smallSlot, RectTransform board, Texture tex)
        {
            var go = new GameObject("OppPeek", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<OppPeek>();
            p.m_rt = (RectTransform)go.transform;
            p.m_rt.anchorMin = p.m_rt.anchorMax = new Vector2(0.5f, 0.5f);
            p.m_rt.pivot = new Vector2(0.5f, 0.5f);
            p.m_parent = parent;
            p.m_small = smallSlot;
            p.m_board = board;
            p.m_image = go.GetComponent<RawImage>();
            p.m_image.texture = tex;
            p.m_image.raycastTarget = true;
            Ui.OwnCanvas(go, true); // 매 프레임 위치를 바꾸므로 자기 캔버스
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color32(0x2a, 0x1d, 0x16, 255);
            outline.effectDistance = new Vector2(3, -3);
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            m_pressed = true;
            m_hold = 0;
        }

        public void OnPointerUp(PointerEventData e) => Release();

        void Release()
        {
            m_pressed = false;
            m_hold = -1;
        }

        /// <summary>누른 채로 꺼지면(판을 나감) 손을 뗀 것으로 - 다음 판에 펼쳐진 채로 시작하지 않게</summary>
        void OnDisable()
        {
            Release();
            Aiming = false;
            m_t = 0;
        }

        void LateUpdate()
        {
            if (m_hold >= 0) m_hold += Time.unscaledDeltaTime;
            bool want = Aiming || (m_pressed && m_hold >= HoldDelay);
            float target = want ? 1 : 0;
            if (m_t != target) m_t = Mathf.MoveTowards(m_t, target, Time.unscaledDeltaTime / Anim);
            Place(); // HUD 배치가 바뀌어도 맞게 매 프레임 (모서리 8개 읽기라 가볍다)
        }

        /// <summary>작은 칸과 큰 칸(전장 가운데) 사이를 m_t 만큼</summary>
        void Place()
        {
            Rect a = LocalRect(m_small);
            Rect bb = LocalRect(m_board);
            float side = Mathf.Min(bb.width, bb.height) * BigFrac;
            var big = new Rect(bb.center.x - side / 2, bb.center.y - side / 2, side, side);
            float k = m_t * m_t * (3 - 2 * m_t);
            Vector2 c = Vector2.Lerp(a.center, big.center, k);
            float s = Mathf.Lerp(Mathf.Min(a.width, a.height), side, k);
            if (m_shake > 0)
            {
                m_shake -= Time.unscaledDeltaTime;
                c.x += Mathf.Sin(m_shake * 90f) * 5f;
            }
            m_rt.anchoredPosition = c - m_parent.rect.center; // 부모 가운데 기준
            m_rt.sizeDelta = new Vector2(s, s);
        }

        Rect LocalRect(RectTransform r)
        {
            r.GetWorldCorners(m_corners);
            Vector3 lo = m_parent.InverseTransformPoint(m_corners[0]), hi = m_parent.InverseTransformPoint(m_corners[2]);
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }
    }
}
