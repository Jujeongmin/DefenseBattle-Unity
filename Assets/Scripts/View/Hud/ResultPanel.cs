using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>결과·확인 창 (ui.ts showResult): 제목 리본 / 별 / 설명 / 보상 / 작은 안내 / 버튼 최대 3개</summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        [SerializeField] GameObject m_root;
        [SerializeField] Image m_ribbon;
        [SerializeField] Sprite m_winRibbon;
        [SerializeField] Sprite m_loseRibbon;
        [SerializeField] TextMeshProUGUI m_title;
        [SerializeField] GameObject m_stars;
        [SerializeField] Image[] m_starImages = new Image[3];
        [SerializeField] Sprite m_starOn;
        [SerializeField] Sprite m_starOff;
        [SerializeField] TextMeshProUGUI m_sub;
        [SerializeField] TextMeshProUGUI m_rewards;
        [SerializeField] TextMeshProUGUI m_extra;
        [SerializeField] Button[] m_buttons = new Button[3];
        [SerializeField] TextMeshProUGUI[] m_buttonTexts = new TextMeshProUGUI[3];
        [SerializeField] Sprite[] m_styleSprites = new Sprite[4]; // ResultStyle 순서
        [SerializeField] Sprite[] m_stylePressed = new Sprite[4];

        static readonly Regex s_tags = new Regex("<[^>]+>");
        ResultView m_view;
        Action<ResultAction> m_onAction;

        public bool IsOpen => m_root.activeSelf;

        void Awake()
        {
            for (int i = 0; i < m_buttons.Length; i++)
            {
                int k = i;
                m_buttons[i].onClick.AddListener(() => Press(k));
            }
            m_root.SetActive(false);
        }

        public void Show(ResultView v, Action<ResultAction> onAction)
        {
            m_view = v;
            m_onAction = onAction;
            m_ribbon.sprite = v.Win ? m_winRibbon : m_loseRibbon;
            m_title.text = v.Title;
            m_stars.SetActive(v.Stars.HasValue);
            for (int i = 0; i < m_starImages.Length; i++) m_starImages[i].sprite = v.Stars.HasValue && i < v.Stars.Value ? m_starOn : m_starOff;
            m_sub.text = v.Sub ?? "";
            m_rewards.text = string.Join("   ", v.Rewards);
            m_rewards.gameObject.SetActive(v.Rewards.Count > 0);
            // 원본 문구에 남은 HTML(<div class="fine">)은 지운다 (TMP 태그가 아님)
            m_extra.text = string.IsNullOrEmpty(v.Extra) ? "" : s_tags.Replace(v.Extra, "");
            m_extra.gameObject.SetActive(!string.IsNullOrEmpty(v.Extra));
            for (int i = 0; i < m_buttons.Length; i++)
            {
                bool used = i < v.Buttons.Count;
                m_buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                ResultButton b = v.Buttons[i];
                m_buttonTexts[i].text = b.Label;
                m_buttons[i].image.sprite = m_styleSprites[(int)b.Style];
                SpriteState st = m_buttons[i].spriteState;
                st.pressedSprite = m_stylePressed[(int)b.Style];
                m_buttons[i].spriteState = st;
            }
            m_root.SetActive(true);
        }

        public void Hide() => m_root.SetActive(false);

        void Press(int i)
        {
            if (m_view == null || i >= m_view.Buttons.Count) return;
            m_onAction?.Invoke(m_view.Buttons[i].Action);
        }
    }
}
