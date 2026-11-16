using System.Collections.Generic;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>다국어 (i18n.ts tl): 표 조회·없으면 원문·@@ 문맥·원본 HTML 정리·언어 고르기</summary>
    public class LocTests
    {
        [TearDown]
        public void TearDown() => Localization.Use("ko");

        [Test]
        public void Lookup_UsesTableOrKorean()
        {
            Localization.Use("en");
            Assert.AreEqual("Summon", Loc.Tl("소환"));
            Assert.AreEqual("이 문장은 표에 없음", Loc.Tl("이 문장은 표에 없음"), "없으면 한국어 원문");
            Assert.AreEqual("Clear wave 30", Loc.Tl("30웨이브 클리어"), "원본 표의 밀린 번역을 바로잡음");
            Localization.Use("ko");
            Assert.AreEqual("영웅", Loc.Tl("영웅@@button"), "한국어는 @@ 문맥을 뗀다");
        }

        [Test]
        public void Html_IsConvertedForTmp()
        {
            Assert.AreEqual("<b>Open!</b>", Localization.Clean("<b class=\"open\">Open!</b>"));
            Assert.AreEqual("a\nb", Localization.Clean("a<br>b"));
            Assert.AreEqual("small note", Localization.Clean("<div class=\"fine\">small note</div>"));
            Assert.AreEqual("x <sprite name=\"gem\">", Localization.Clean("x <sprite name=\"gem\">"), "TMP 스프라이트는 그대로");
        }

        [Test]
        public void Device_PicksLanguage()
        {
            Assert.AreEqual("ko", Localization.FromSystem(SystemLanguage.Korean));
            Assert.AreEqual("ja", Localization.FromSystem(SystemLanguage.Japanese));
            Assert.AreEqual("zh-Hans", Localization.FromSystem(SystemLanguage.ChineseSimplified));
            Assert.AreEqual("zh-Hant", Localization.FromSystem(SystemLanguage.ChineseTraditional));
            Assert.AreEqual("en", Localization.FromSystem(SystemLanguage.French), "없는 언어는 영어");
        }

        [Test]
        public void EveryLanguageLoads()
        {
            foreach (string lang in new List<string> { "en", "ja", "zh-Hans", "zh-Hant" })
            {
                Localization.Use(lang);
                Assert.AreNotEqual("소환", Loc.Tl("소환"), lang);
            }
        }
    }
}
