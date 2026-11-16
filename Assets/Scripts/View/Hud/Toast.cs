using TMPro;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>화면 아래쪽 짧은 안내 (원본 toast): 2초 보이고 사라진다</summary>
    public sealed class Toast : MonoBehaviour
    {
        [SerializeField] CanvasGroup m_group;
        [SerializeField] TextMeshProUGUI m_text;

        const float Duration = 2f, Fade = 0.3f;
        float m_left;

        public void Show(string message)
        {
            m_text.text = message;
            m_left = Duration;
            m_group.alpha = 1;
            if (!m_group.gameObject.activeSelf) m_group.gameObject.SetActive(true);
        }

        void Update()
        {
            if (m_left <= 0) return;
            m_left -= Time.unscaledDeltaTime;
            m_group.alpha = Mathf.Clamp01(m_left / Fade);
            if (m_left <= 0) m_group.gameObject.SetActive(false);
        }
    }
}
