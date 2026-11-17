using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 튜토리얼 스포트라이트 (tutorial.ts Spotlight): 화면을 어둡게 하고 눌러야 할 곳만 밝힌다.
    /// 밝힌 곳 둘레를 어두운 판 4장이 덮어 다른 곳은 눌리지 않고, 구멍으로는 진짜 버튼이 눌린다 (SpotTap 으로 탭을 안다).
    /// 대상이 움직여도(패널이 올라오는 중 등) 매 프레임 따라간다 - 바뀔 때만 위치를 고쳐 할당 없음.
    /// 모든 화면 위에 뜨도록 자기 Canvas 를 만든다 (열 때 만들고 닫을 때 지운다).
    /// </summary>
    public sealed class Spotlight : MonoBehaviour
    {
        const float Pad = 6;
        static readonly Color32 Gold = new Color32(0xff, 0xc9, 0x28, 255), GoldLight = new Color32(0xff, 0xf2, 0xa8, 255);
        static readonly Color32 DimColor = new Color32(10, 6, 4, 190);

        RectTransform m_root;
        readonly Image[] m_dim = new Image[4];
        readonly Image[] m_edge = new Image[4];
        RectTransform m_hand;
        Image m_blocker;
        RectTransform m_bubble;
        TextMeshProUGUI m_text;
        readonly Vector3[] m_corners = new Vector3[4];

        Func<GameObject> m_target;
        Action m_tapped;
        GameObject m_bound;
        SpotTap m_tap;
        Camera m_boundCam;
        Rect m_hole = new Rect(-1, -1, 0, 0);
        bool m_hasHole = true;
        float m_msgLeft = -1;
        Action m_msgDone;
        Vector2 m_handBase;

        /// <summary>overlay 의 CanvasScaler 설정을 그대로 따라 만든다 (글자·손 크기가 다른 UI 와 같게)</summary>
        public static Spotlight Create(CanvasScaler like)
        {
            var go = new GameObject("Spotlight", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.GetComponent<CanvasScaler>();
            if (like != null)
            {
                scaler.uiScaleMode = like.uiScaleMode;
                scaler.referenceResolution = like.referenceResolution;
                scaler.screenMatchMode = like.screenMatchMode;
                scaler.matchWidthOrHeight = like.matchWidthOrHeight;
            }
            var s = go.AddComponent<Spotlight>();
            s.Build();
            return s;
        }

        void Build()
        {
            m_root = (RectTransform)transform;
            for (int i = 0; i < 4; i++)
            {
                m_dim[i] = Ui.Image(m_root, null, DimColor, "Dim");
                m_dim[i].raycastTarget = true; // 밝힌 곳 밖은 막는다
            }
            for (int i = 0; i < 4; i++) m_edge[i] = Ui.Image(m_root, null, Gold, "Edge");
            // 탭한 뒤 다음 단계가 뜰 때까지 화면 전체를 막는다 (빠른 두 번 탭이 구멍으로 한 번 더 들어가 단계를 건너뛰지 않게 - 원본은 탭 뒤 onTap 을 비움)
            m_blocker = Ui.Image(m_root, null, new Color(0, 0, 0, 0), "Blocker");
            m_blocker.raycastTarget = true;
            Ui.Fill(m_blocker.rectTransform);
            m_blocker.enabled = false;
            Image hand = Ui.Image(m_root, Ui.Cat.Icon("pointer"), Color.white, "Hand");
            hand.preserveAspect = true;
            m_hand = hand.rectTransform;
            m_hand.anchorMin = m_hand.anchorMax = Vector2.zero;
            m_hand.sizeDelta = new Vector2(76, 76);
            m_hand.pivot = new Vector2(0.5f, 0);

            Image bubble = Ui.Frame(m_root, "paper", 1.4f, "Bubble");
            m_bubble = bubble.rectTransform;
            m_bubble.anchorMin = new Vector2(0.06f, 0); // 너비 = 화면의 88% (첫 프레임 캔버스 크기와 무관하게)
            m_bubble.anchorMax = new Vector2(0.94f, 0);
            m_bubble.sizeDelta = Vector2.zero;
            Ui.Column(bubble.transform, 0, 30, 30, 22, 24, TextAnchor.MiddleCenter);
            var fit = bubble.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_text = Ui.Text(bubble.transform, "", 30, Ui.Ink, TextAlignmentOptions.Center, true);
            ShowHole(false); // 대상을 찾기 전엔 화면 전체를 어둡게
        }

        /// <summary>한 단계: target 을 밝히고, 그곳을 누르면 tapped (대상이 아직 없으면 나타날 때까지 기다린다)</summary>
        public void Step(Func<GameObject> target, string text, Action tapped)
        {
            Unbind();
            m_blocker.enabled = false;
            m_target = target;
            m_tapped = tapped;
            m_msgLeft = -1;
            m_msgDone = null;
            m_text.text = text;
            m_hole = new Rect(-1, -1, 0, 0);
        }

        /// <summary>버튼 없이 안내만 잠깐 (화면 전체를 어둡게)</summary>
        public void Message(string text, float seconds, Action done)
        {
            Unbind();
            m_target = null;
            m_tapped = null;
            m_text.text = text;
            m_msgLeft = seconds;
            m_msgDone = done;
        }

        public void Close()
        {
            Unbind();
            m_target = null;
            Destroy(gameObject);
        }

        void Unbind()
        {
            if (m_tap != null) Destroy(m_tap);
            m_tap = null;
            m_bound = null;
        }

        void OnTap()
        {
            // 꺼진 버튼을 눌렀으면 넘어가지 않는다
            if (m_bound == null || (m_bound.TryGetComponent(out Selectable sel) && !sel.IsInteractable())) return;
            Action t = m_tapped;
            m_tapped = null;
            m_blocker.enabled = true;
            Unbind();
            m_target = null;
            t?.Invoke();
        }

        void Update()
        {
            // 손·테두리 깜빡임 (steps(2))
            bool odd = (int)(Time.unscaledTime / 0.4f) % 2 == 1;
            Color32 ec = odd ? GoldLight : Gold;
            if (!m_edge[0].color.Equals((Color)ec)) for (int i = 0; i < 4; i++) m_edge[i].color = ec;

            if (m_msgLeft >= 0)
            {
                ShowHole(false);
                PlaceBubble(m_root.rect.height * 0.42f, true);
                m_msgLeft -= Time.unscaledDeltaTime;
                if (m_msgLeft < 0)
                {
                    Action d = m_msgDone;
                    m_msgDone = null;
                    d?.Invoke();
                }
                return;
            }

            GameObject t = m_target?.Invoke();
            if (t == null || !t.activeInHierarchy)
            {
                if (m_bound != null) Unbind();
                if (!m_hasHole) PlaceBubble(m_root.rect.height * 0.42f, true);
                return; // 나타날 때까지 지난 모습 그대로
            }
            m_hand.anchoredPosition = new Vector2(m_handBase.x, m_handBase.y + (odd ? 10 : 0)); // 손 까딱
            if (t != m_bound) Bind(t);

            ((RectTransform)t.transform).GetWorldCorners(m_corners);
            Vector2 a = ToLocal(m_corners[0]), b = ToLocal(m_corners[2]);
            var hole = Rect.MinMaxRect(a.x - Pad, a.y - Pad, b.x + Pad, b.y + Pad);
            if (hole != m_hole) Layout(hole);
        }

        void Bind(GameObject t)
        {
            Unbind();
            m_bound = t;
            m_tap = t.AddComponent<SpotTap>();
            m_tap.Tapped = OnTap;
            Canvas c = t.GetComponentInParent<Canvas>();
            m_boundCam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            ScrollIntoView(t);
        }

        /// <summary>대상이 스크롤 칸 밖이면 한 번 보이게 끌어온다</summary>
        static void ScrollIntoView(GameObject t)
        {
            ScrollRect sr = t.GetComponentInParent<ScrollRect>();
            if (sr == null || sr.content == null || !sr.vertical) return;
            RectTransform view = sr.viewport != null ? sr.viewport : (RectTransform)sr.transform;
            var rt = (RectTransform)t.transform;
            Vector3 center = view.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            Rect vr = view.rect;
            if (center.y >= vr.yMin && center.y <= vr.yMax) return;
            Vector2 p = sr.content.anchoredPosition;
            p.y += vr.center.y - center.y;
            sr.content.anchoredPosition = p;
            sr.velocity = Vector2.zero;
        }

        /// <summary>월드 점 → 이 캔버스 왼쪽 아래 기준 좌표</summary>
        Vector2 ToLocal(Vector3 world)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(m_boundCam, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_root, screen, null, out Vector2 local);
            Rect r = m_root.rect;
            return new Vector2(local.x - r.xMin, local.y - r.yMin);
        }

        void Layout(Rect h)
        {
            m_hole = h;
            ShowHole(true);
            Rect r = m_root.rect;
            float w = r.width, ht = r.height;
            Set(m_dim[0].rectTransform, 0, h.yMax, w, ht);           // 위
            Set(m_dim[1].rectTransform, 0, 0, w, h.yMin);            // 아래
            Set(m_dim[2].rectTransform, 0, h.yMin, h.xMin, h.yMax);  // 왼쪽
            Set(m_dim[3].rectTransform, h.xMax, h.yMin, w, h.yMax);  // 오른쪽
            const float e = 5;
            Set(m_edge[0].rectTransform, h.xMin - e, h.yMax, h.xMax + e, h.yMax + e);
            Set(m_edge[1].rectTransform, h.xMin - e, h.yMin - e, h.xMax + e, h.yMin);
            Set(m_edge[2].rectTransform, h.xMin - e, h.yMin, h.xMin, h.yMax);
            Set(m_edge[3].rectTransform, h.xMax, h.yMin, h.xMax + e, h.yMax);

            m_handBase = new Vector2(h.center.x, h.yMax + 8);
            m_hand.anchoredPosition = m_handBase;
            // 안내 글: 대상이 화면 위쪽이면 아래에, 아래쪽이면 위에
            bool below = h.center.y > ht * 0.55f;
            PlaceBubble(below ? h.yMin - 24 : h.yMax + 96, !below);
        }

        void PlaceBubble(float y, bool above)
        {
            Vector2 pivot = new Vector2(0.5f, above ? 0 : 1);
            if (m_bubble.pivot != pivot) m_bubble.pivot = pivot;
            Vector2 p = new Vector2(0, y);
            if (m_bubble.anchoredPosition != p) m_bubble.anchoredPosition = p;
        }

        void ShowHole(bool on)
        {
            if (m_hasHole == on) return;
            m_hasHole = on;
            for (int i = 0; i < 4; i++) m_edge[i].enabled = on;
            m_hand.gameObject.SetActive(on);
            if (!on)
            {
                Ui.Fill(m_dim[0].rectTransform);
                for (int i = 1; i < 4; i++) Set(m_dim[i].rectTransform, 0, 0, 0, 0);
                m_hole = new Rect(-1, -1, 0, 0);
            }
        }

        /// <summary>왼쪽 아래 기준 (x0,y0)~(x1,y1)</summary>
        static void Set(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x0, y0);
            rt.sizeDelta = new Vector2(Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }
    }
}
