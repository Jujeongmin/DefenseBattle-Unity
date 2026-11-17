using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 첫 대전: 몹 카드를 끌어 던지는 법 (ui.ts showThrowHint): 손가락이 던질 수 있는 카드에서 상대 미니맵으로 되풀이해 움직인다.
    /// 한 번 던질 때까지 소환을 잠근다 (소환에 골드를 다 써서 못 던지는 일이 없게). 던질 수 있는 카드가 없으면 잠금을 푼다.
    /// </summary>
    public sealed class ThrowHint
    {
        const float Loop = 1.3f;

        readonly RectTransform m_parent;
        readonly RectTransform m_root, m_hand;
        readonly TextMeshProUGUI m_text;
        readonly Vector3[] m_corners = new Vector3[4];
        float m_t;

        public bool Active { get; private set; }

        public ThrowHint(RectTransform parent)
        {
            m_parent = parent;
            m_root = Ui.Rect(parent, "ThrowHint");
            Ui.Fill(m_root);
            Image bubble = Ui.Frame(m_root, "paper", 1.4f, "Bubble");
            RectTransform b = bubble.rectTransform;
            b.anchorMin = new Vector2(0.15f, 0.5f);
            b.anchorMax = new Vector2(0.85f, 0.5f);
            b.sizeDelta = new Vector2(0, 110);
            b.anchoredPosition = new Vector2(0, 40);
            m_text = Ui.Text(b, "", 28, Ui.Ink, TextAlignmentOptions.Center, true);
            Ui.Fill(m_text.rectTransform, 16, 10, 16, 10);
            Image hand = Ui.Image(m_root, Ui.Cat.Icon("pointer"), Color.white, "Hand");
            hand.preserveAspect = true;
            m_hand = hand.rectTransform;
            m_hand.anchorMin = m_hand.anchorMax = new Vector2(0.5f, 0.5f);
            m_hand.sizeDelta = new Vector2(80, 80);
            m_root.gameObject.SetActive(false);
            Ui.OwnCanvas(m_root.gameObject); // 손이 매 프레임 움직인다
        }

        public void Show()
        {
            Active = true;
            m_t = 0;
            m_text.text = Loc.Tl("카드를 끌어서<br><b>상대 전장</b>에 던지세요!");
        }

        public void Hide()
        {
            Active = false;
            m_root.gameObject.SetActive(false);
        }

        /// <summary>card = 던질 수 있는 카드 (없으면 null → 숨김), target = 상대 미니맵</summary>
        public void Tick(float dt, RectTransform card, RectTransform target, bool paused)
        {
            bool show = Active && card != null && !paused;
            if (m_root.gameObject.activeSelf != show) m_root.gameObject.SetActive(show);
            if (!show) return;
            if (m_root.GetSiblingIndex() != m_parent.childCount - 1) m_root.SetAsLastSibling();
            m_t = (m_t + dt / Loop) % 1f;
            Vector2 a = Center(card), b = Center(target);
            float k = Mathf.Clamp01(m_t * 1.25f); // 끝에서 잠깐 멈춤
            k = k * k * (3 - 2 * k);
            m_hand.anchoredPosition = Vector2.Lerp(a, b, k) + new Vector2(20, -30);
        }

        Vector2 Center(RectTransform r)
        {
            r.GetWorldCorners(m_corners);
            Vector3 c = m_parent.InverseTransformPoint((m_corners[0] + m_corners[2]) * 0.5f);
            return new Vector2(c.x, c.y) - m_parent.rect.center;
        }
    }
}
