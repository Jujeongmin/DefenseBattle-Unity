using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전투 화면이 쓰는 그림·폰트 목록 (Tools → Defense Battle → Build Battle Assets 가 채운다).
    /// 시트 한 장 = 12프레임, 인덱스 = 방향*3 + 열 (방향 0=하 1=좌 2=우 3=상, 열 1 = 서 있기).
    /// 조회는 배열 인덱스라 매 프레임 불러도 할당이 없다.
    /// </summary>
    public sealed class SpriteCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/Art/DefenseBattle/Generated/SpriteCatalog.asset";

        /// <summary>시트 한 장의 12프레임</summary>
        [Serializable]
        public sealed class Sheet
        {
            public Sprite[] Frames = new Sprite[0];
        }

        /// <summary>한 종족의 몹 시트들 (변형 번호 = 인덱스, 없는 번호는 빈 시트)</summary>
        [Serializable]
        public sealed class RaceSheets
        {
            public Sheet[] Variants = new Sheet[0];
        }

        [SerializeField] Sheet[] m_units = new Sheet[0];
        [SerializeField] RaceSheets[] m_mobs = new RaceSheets[0];
        [SerializeField] Sprite m_field;
        [SerializeField] Sprite m_coin;
        [SerializeField] Sprite[] m_icons = new Sprite[0];
        [SerializeField] TMP_FontAsset m_font;
        [SerializeField] TMP_FontAsset m_boldFont;
        [SerializeField] TMP_FontAsset m_numberFont;
        /// <summary>FusionPixel ja / zh-Hans / zh-Hant (SDF, 글꼴 폴백)</summary>
        [SerializeField] TMP_FontAsset[] m_cjkFonts = new TMP_FontAsset[3];
        [SerializeField] Sprite[] m_frames = new Sprite[0];
        [SerializeField] Material m_hudTextMaterial;
        Dictionary<string, Sprite> m_frameByName;

        /// <summary>언어별 폴백 글꼴 (한국어·영어는 null)</summary>
        public TMP_FontAsset CjkFont(string lang)
        {
            if (m_cjkFonts == null || m_cjkFonts.Length < 3) return null;
            switch (lang)
            {
                case "ja": return m_cjkFonts[0];
                case "zh-Hans": return m_cjkFonts[1];
                case "zh-Hant": return m_cjkFonts[2];
                default: return null;
            }
        }

        /// <summary>UI 테두리 그림 (UI/Frame 파일 이름: btn_red, paper …), 없으면 null</summary>
        public Sprite Frame(string name)
        {
            if (m_frameByName == null)
            {
                m_frameByName = new Dictionary<string, Sprite>(m_frames.Length);
                foreach (Sprite s in m_frames) if (s != null) m_frameByName[s.name] = s;
            }
            return m_frameByName.TryGetValue(name, out Sprite sprite) ? sprite : null;
        }

        /// <summary>HUD 글자 머티리얼 (얇은 검은 테두리)</summary>
        public Material HudTextMaterial => m_hudTextMaterial;

        Dictionary<string, Sprite> m_iconByName;

        /// <summary>미리 구운 전장 배경 (720×720, PPU 32)</summary>
        public Sprite Field => m_field;
        public Sprite Coin => m_coin;
        public TMP_FontAsset Font => m_font;
        public TMP_FontAsset BoldFont => m_boldFont;
        /// <summary>전장 위 숫자·이름표 (SDF - 검은 테두리를 쓰려고)</summary>
        public TMP_FontAsset NumberFont => m_numberFont;

        /// <summary>유닛 시트 (직업 × 판 안 등급)</summary>
        public Sprite[] Unit(Job job, int grade) => m_units[(int)job * Grades.Count + grade].Frames;

        /// <summary>몹 시트 - 없는 변형이면 그 종족의 0번</summary>
        public Sprite[] Mob(Race race, int variant)
        {
            Sheet[] v = m_mobs[(int)race].Variants;
            if (variant >= 0 && variant < v.Length && v[variant] != null && v[variant].Frames.Length > 0) return v[variant].Frames;
            return v[0].Frames;
        }

        /// <summary>UI 아이콘 (UI/Icons 파일 이름), 없으면 null</summary>
        public Sprite Icon(string name)
        {
            if (m_iconByName == null)
            {
                m_iconByName = new Dictionary<string, Sprite>(m_icons.Length);
                foreach (Sprite s in m_icons) if (s != null) m_iconByName[s.name] = s;
            }
            return m_iconByName.TryGetValue(name, out Sprite sprite) ? sprite : null;
        }

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용: 내용 채우기</summary>
        public void EditorFillUi(TMP_FontAsset[] cjk, Sprite[] frames, Material hudText)
        {
            m_cjkFonts = cjk;
            m_frames = frames;
            m_hudTextMaterial = hudText;
            m_frameByName = null;
        }

        public void EditorFill(Sheet[] units, RaceSheets[] mobs, Sprite field, Sprite coin, Sprite[] icons, TMP_FontAsset font, TMP_FontAsset bold, TMP_FontAsset number)
        {
            m_numberFont = number;
            m_units = units;
            m_mobs = mobs;
            m_field = field;
            m_coin = coin;
            m_icons = icons;
            m_font = font;
            m_boldFont = bold;
            m_iconByName = null;
        }
#endif
    }
}
