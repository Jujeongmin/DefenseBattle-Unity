using DefenseBattle.Core;
using TMPro;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 씬에 놓인 고정 라벨: 한국어 원문 키를 들고 있다가 켜질 때·언어가 바뀔 때 번역해 넣는다
    /// (씬 빌더가 한국어로 구워 두면 다른 언어에서 그대로 남는 문제 - 2단계 리뷰).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocText : MonoBehaviour
    {
        [SerializeField] string m_key;
        /// <summary>번역 앞에 붙일 것 (아이콘 등, 번역하지 않음)</summary>
        [SerializeField] string m_prefix;
        [SerializeField] string m_suffix;

        TMP_Text m_text;

        public void Set(string key, string prefix = "", string suffix = "")
        {
            m_key = key;
            m_prefix = prefix;
            m_suffix = suffix;
            Apply();
        }

        void OnEnable()
        {
            Localization.Changed += Apply;
            Apply();
        }

        void OnDisable() => Localization.Changed -= Apply;

        void Apply()
        {
            if (string.IsNullOrEmpty(m_key)) return;
            if (m_text == null) m_text = GetComponent<TMP_Text>();
            m_text.text = m_prefix + Loc.Tl(m_key) + m_suffix;
        }
    }
}
