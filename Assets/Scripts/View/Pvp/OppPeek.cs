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

        // style.css #opp-canvas: 진한 테두리 3 + 바깥 갈색 고리 2, 펼치면 고리가 빨강(조준)/금색(구경)이고 주변이 어두워진다
        static readonly Color32 RingSmall = new Color32(0x8a, 0x6f, 0x55, 255), RingAim = new Color32(0xe5, 0x48, 0x4d, 255),
            RingPeek = new Color32(0xff, 0xc9, 0x28, 255), EdgeColor = new Color32(0x2a, 0x1d, 0x16, 255), DimColor = new Color32(10, 6, 4, 115);

        RectTransform m_rt, m_small, m_board, m_parent, m_map;
        Image m_ring, m_dim;
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
        public RectTransform View => m_map;

        /// <summary>던진 몹이 닿음 → 잠깐 흔들림 (ui.ts opp-canvas .hit)</summary>
        public void Hit() => m_shake = 0.25f;

        public static OppPeek Create(RectTransform parent, RectTransform smallSlot, RectTransform board, Texture tex)
        {
            // 펼쳤을 때 주변을 어둡게 하는 막 (미니맵 바로 뒤)
            Image dim = Ui.Image(parent, null, DimColor, "OppDim");
            Ui.Fill(dim.rectTransform);
            dim.gameObject.SetActive(false);
            Ui.OwnCanvas(dim.gameObject);

            var go = new GameObject("OppPeek", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<OppPeek>();
            p.m_dim = dim;
            p.m_ring = go.GetComponent<Image>();
            p.m_ring.color = RingSmall;
            p.m_ring.raycastTarget = true;
            Image edge = Ui.Image(go.transform, null, EdgeColor, "Edge");
            Ui.Fill(edge.rectTransform, 3, 3, 3, 3);
            RawImage map = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            map.transform.SetParent(edge.transform, false);
            p.m_map = map.rectTransform;
            Ui.Fill(p.m_map, 3, 3, 3, 3);
            map.raycastTarget = false;
            p.m_rt = (RectTransform)go.transform;
            p.m_rt.anchorMin = p.m_rt.anchorMax = new Vector2(0.5f, 0.5f);
            p.m_rt.pivot = new Vector2(0.5f, 0.5f);
            p.m_parent = parent;
            p.m_small = smallSlot;
            p.m_board = board;
            p.m_image = map;
            p.m_image.texture = tex;
            Ui.OwnCanvas(go, true); // 매 프레임 위치를 바꾸므로 자기 캔버스
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
            if (m_dim != null) m_dim.gameObject.SetActive(false);
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
            // 고리 색·주변 어둡게 (펼치는 동안만 바뀐다)
            Color32 ring = k <= 0 ? RingSmall : Aiming ? RingAim : RingPeek;
            if (!m_ring.color.Equals((Color)ring)) m_ring.color = ring;
            bool dim = k > 0;
            if (m_dim.gameObject.activeSelf != dim) m_dim.gameObject.SetActive(dim);
            if (dim)
            {
                if (m_dim.transform.GetSiblingIndex() != m_rt.GetSiblingIndex() - 1) m_dim.transform.SetSiblingIndex(m_rt.GetSiblingIndex());
                Color dc = DimColor;
                dc.a *= k;
                if (m_dim.color != dc) m_dim.color = dc;
            }
        }

        Rect LocalRect(RectTransform r)
        {
            r.GetWorldCorners(m_corners);
            Vector3 lo = m_parent.InverseTransformPoint(m_corners[0]), hi = m_parent.InverseTransformPoint(m_corners[2]);
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }
    }
}
