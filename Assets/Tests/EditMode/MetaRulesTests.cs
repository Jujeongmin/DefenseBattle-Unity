using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>퀘스트·시즌 패스·월정액 받을 것 판정 (menu.ts questClaimable·seasonClaimable, shop.ts canClaimMonthly·monthlyDaysLeft)</summary>
    public class MetaRulesTests
    {
        static EconState Fresh() => EconState.Fresh("S2026-11");
        const string Today = "2026-11-16";
        const long Day = 86_400_000L;
        const long Now = 1_794_800_000_000L;

        [Test]
        public void Quest_ClaimableWhenGoalReachedAndNotClaimedToday()
        {
            EconState e = Fresh();
            DailyQuestDef q = Quests.Daily[0];
            var progress = new Dictionary<string, int> { { q.Key, q.Goal - 1 } };
            Assert.IsFalse(MetaRules.QuestClaimable(e, progress, Today));
            progress[q.Key] = q.Goal;
            Assert.IsTrue(MetaRules.QuestClaimable(e, progress, Today));
            e.OneTime.Add("quest:" + Today + ":" + q.Key); // 서버 EconRules.ClaimQuest 의 1회 키
            Assert.IsTrue(MetaRules.QuestClaimed(e, q.Key, Today));
            Assert.IsFalse(MetaRules.QuestClaimable(e, progress, Today));
        }

        [Test]
        public void Season_ClaimableReachedTiersOnly()
        {
            EconState e = Fresh();
            Assert.IsFalse(MetaRules.SeasonClaimable(e));
            e.Season.Xp = Season.XpPerTier * 2;
            Assert.IsTrue(MetaRules.SeasonClaimable(e));
            e.Season.ClaimedFree.AddRange(new[] { 1, 2 });
            Assert.IsFalse(MetaRules.SeasonClaimable(e), "프리미엄이 없으면 무료만");
            e.Season.Premium = true;
            Assert.IsTrue(MetaRules.SeasonClaimable(e));
        }

        [Test]
        public void Monthly_ActiveAndOncePerDay()
        {
            EconState e = Fresh();
            Assert.IsFalse(MetaRules.MonthlyClaimable(e, Now, Today));
            e.Monthly.Expires = Now + 3 * Day - 1;
            Assert.IsTrue(MetaRules.MonthlyClaimable(e, Now, Today));
            Assert.AreEqual(3, MetaRules.MonthlyDaysLeft(e, Now));
            e.Monthly.LastClaim = Today;
            Assert.IsFalse(MetaRules.MonthlyClaimable(e, Now, Today));
        }

        [Test]
        public void SeasonResetPreview()
        {
            Assert.AreEqual(400, PvpConfig.SeasonResetTrophies(400));
            Assert.AreEqual(550, PvpConfig.SeasonResetTrophies(600), "500 넘는 만큼 절반");
        }
    }
}
