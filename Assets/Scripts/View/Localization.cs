using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DefenseBattle.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 다국어 (i18n.ts): 한국어 원문을 키로 쓰는 번역 표 (Resources/Loc/{언어}.json, tools/i18n_to_json.py 가 만든다).
    /// 언어 = 저장값 > 기기 언어 > 영어. Core.Loc.Translate 에 연결해 Loc.Tl 이 번역을 쓴다. 없는 문장은 한국어 원문.
    /// 원본 문장의 HTML(&lt;b class&gt;, &lt;br&gt;, &lt;div&gt;)은 TMP 에 맞게 정리한다.
    /// </summary>
    public static class Localization
    {
        public const string LangKey = "lang";
        /// <summary>설정 화면 순서와 이름 (i18n.ts LANGS)</summary>
        public static readonly string[] Ids = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        public static readonly string[] Names = { "한국어", "English", "日本語", "简体中文", "繁體中文" };

        static readonly Regex s_br = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase);
        static readonly Regex s_tagWithAttr = new Regex(@"<(b|i|u|s)\s+[^>]*>", RegexOptions.IgnoreCase);
        static readonly Regex s_strip = new Regex(@"</?(div|span|p)(\s+[^>]*)?>", RegexOptions.IgnoreCase);

        static Dictionary<string, string> s_table;
        static readonly Dictionary<string, string> s_cache = new Dictionary<string, string>();

        public static string Current { get; private set; } = "ko";

        /// <summary>언어가 바뀜 (LocText·열린 화면이 다시 그린다)</summary>
        public static event Action Changed;

        static SpriteCatalog s_fonts;

        /// <summary>앱 시작: 저장값 또는 기기 언어. 일본어·중국어는 FusionPixel 을 글꼴 폴백으로 붙인다</summary>
        public static void Init(SpriteCatalog fonts)
        {
            s_fonts = fonts;
            string saved = PlayerPrefs.GetString(LangKey, "");
            Use(Array.IndexOf(Ids, saved) >= 0 ? saved : FromSystem(Application.systemLanguage));
        }

        /// <summary>언어 바꾸고 저장 (화면은 다시 그려야 반영 - 원본은 새로고침)</summary>
        public static void Save(string lang)
        {
            PlayerPrefs.SetString(LangKey, lang);
            PlayerPrefs.Save();
            Use(lang);
        }

        public static void Use(string lang)
        {
            Current = Array.IndexOf(Ids, lang) >= 0 ? lang : "en";
            s_cache.Clear();
            s_table = null;
            if (Current != "ko")
            {
                var asset = Resources.Load<TextAsset>("Loc/" + Current);
                if (asset != null) s_table = JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text);
            }
            Loc.Translate = Translate;
            ApplyFontFallback();
            Changed?.Invoke();
        }

        /// <summary>Galmuri 에 없는 한자·간체·번체 글자를 FusionPixel 로 (style.css html[lang] --font 순서)</summary>
        static void ApplyFontFallback()
        {
            if (s_fonts == null) return;
            TMPro.TMP_FontAsset cjk = s_fonts.CjkFont(Current);
            foreach (TMPro.TMP_FontAsset f in new[] { s_fonts.Font, s_fonts.BoldFont, s_fonts.NumberFont })
            {
                if (f == null) continue;
                if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMPro.TMP_FontAsset>();
                f.fallbackFontAssetTable.Clear();
                if (cjk != null) f.fallbackFontAssetTable.Add(cjk);
            }
        }

        static string Translate(string key)
        {
            if (s_cache.TryGetValue(key, out string done)) return done;
            string s = s_table != null && s_table.TryGetValue(key, out string t) ? t : key;
            s = Clean(s);
            s_cache[key] = s;
            return s;
        }

        /// <summary>원본 HTML → TMP: &lt;br&gt; 은 줄바꿈, 속성 붙은 &lt;b class=..&gt; 는 &lt;b&gt;, div·span 은 지움</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            s = s_br.Replace(s, "\n");
            s = s_tagWithAttr.Replace(s, "<$1>");
            return s_strip.Replace(s, "");
        }

        /// <summary>기기 언어 → 게임 언어 (없는 언어면 영어)</summary>
        public static string FromSystem(SystemLanguage l)
        {
            switch (l)
            {
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                default: return "en";
            }
        }
    }
}
