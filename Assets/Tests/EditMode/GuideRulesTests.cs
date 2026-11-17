using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>로비 안내 순서 (menu.ts maybeGuide) · 판 안 팁 (ui.ts maybeTip)</summary>
    public class GuideRulesTests
    {
        static EconState Cleared(params string[] stages)
        {
            EconState e = EconState.Fresh("S2026-11");
            foreach (string s in stages) e.Stages[s] = 1;
            return e;
        }

        static bool All(string key) => true;

        [Test]
        public void LobbyGuide_OnlyAfterTutorialAndInOrder()
        {
            var seen = new List<string>();
            Assert.IsNull(GuideRules.NextLobbyGuide(Cleared(), 2, seen, All), "1-1 전");
            Assert.IsNull(GuideRules.NextLobbyGuide(Cleared("1-1"), 1, seen, All), "튜토리얼 판을 아직 못 깸");
            EconState e = Cleared("1-1");
            Assert.AreEqual("pvp", GuideRules.NextLobbyGuide(e, 2, seen, All));
            seen.Add("pvp");
            Assert.AreEqual("units", GuideRules.NextLobbyGuide(e, 2, seen, All));
            seen.Add("units");
            Assert.AreEqual("campaign", GuideRules.NextLobbyGuide(e, 2, seen, All));
            seen.Add("campaign");
            Assert.IsNull(GuideRules.NextLobbyGuide(e, 2, seen, All), "연구는 1-2 클리어 뒤");
        }

        [Test]
        public void LobbyGuide_SkipsCampaignAfter1_2AndMissingButtons()
        {
            var seen = new List<string> { "pvp", "units" };
            EconState e = Cleared("1-1", "1-2");
            Assert.AreEqual("research", GuideRules.NextLobbyGuide(e, 2, seen, All), "1-2 를 깼으면 캠페인 안내는 건너뜀");
            Assert.IsNull(GuideRules.NextLobbyGuide(e, 2, seen, k => k != "research"), "버튼이 화면에 없으면 다음 기회로");
        }

        [Test]
        public void Tips_GapFirstThenPvpOnly()
        {
            var seen = new List<string>();
            Assert.IsNull(GuideRules.PickTip(1, 2, 10, false, 1, seen), "유닛 3명 이상");
            Assert.IsNull(GuideRules.PickTip(1, 3, 5, false, 1, seen), "6초 뒤");
            Assert.AreEqual(GuideRules.TipGap, GuideRules.PickTip(1, 3, 7, false, 1, seen));
            seen.Add("tip:gap");
            Assert.IsNull(GuideRules.PickTip(1, 3, 30, false, 9, seen), "대전 팁은 대전에서만");
            Assert.AreEqual(GuideRules.TipPeek, GuideRules.PickTip(0, 0, 26, true, 1, seen));
            seen.Add("tip:peek");
            Assert.AreEqual(GuideRules.TipLate, GuideRules.PickTip(0, 0, 26, true, 8, seen));
        }
    }
}
