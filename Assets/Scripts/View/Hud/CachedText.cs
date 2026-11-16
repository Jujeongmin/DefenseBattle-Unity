using System;
using TMPro;

namespace DefenseBattle.View
{
    /// <summary>
    /// TMP 글자를 값이 바뀔 때만 다시 넣는다 (CLAUDE.md: TMP 텍스트는 값이 바뀔 때만 갱신).
    /// char[] 로 넣으면 문자열을 만들지 않는다.
    /// </summary>
    public sealed class CachedText
    {
        readonly TMP_Text m_text;
        char[] m_chars = new char[64];
        int m_len = -1;
        string m_str;

        public CachedText(TMP_Text text) => m_text = text;

        public TMP_Text Text => m_text;
        /// <summary>실제로 TMP 에 넣은 횟수 (테스트용)</summary>
        public int SetCount { get; private set; }

        public void Set(char[] buf, int len)
        {
            if (m_str == null && m_len == len)
            {
                bool same = true;
                for (int i = 0; i < len && same; i++) same = m_chars[i] == buf[i];
                if (same) return;
            }
            if (m_chars.Length < len) m_chars = new char[len * 2];
            Array.Copy(buf, m_chars, len);
            m_len = len;
            m_str = null;
            m_text.SetCharArray(m_chars, 0, len);
            SetCount++;
        }

        public void Set(string s)
        {
            if (m_str != null && string.Equals(m_str, s, StringComparison.Ordinal)) return;
            m_str = s ?? "";
            m_len = -1;
            m_text.text = m_str;
            SetCount++;
        }
    }
}
