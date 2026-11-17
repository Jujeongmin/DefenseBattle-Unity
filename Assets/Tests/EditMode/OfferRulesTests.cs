using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>결제 팝업 조건 (offers.ts): 한정 상품 하루 한 번·24시간, 판 끝 우선순위, 유닛 패키지 등장, 보석 부족 상품</summary>
    public class OfferRulesTests
    {
        const string Sid = "S2026-11";
        const long Hour = 3_600_000L;
        const long Now = 1_794_800_000_000L;

        static EconState Fresh() => EconState.Fresh(Sid);

        [Test]
        public void TryOffer_OncePerDayAndActiveFor24Hours()
        {
            var offers = new Dictionary<string, long>();
            var shown = new Dictionary<string, long>();
            Assert.AreEqual("offer_comeback", OfferRules.TryOffer(offers, shown, "comeback", Now));
            Assert.IsTrue(OfferRules.Active(offers, "offer_comeback", Now + 23 * Hour));
            Assert.IsFalse(OfferRules.Active(offers, "offer_comeback", Now + 24 * Hour));
            Assert.IsNull(OfferRules.TryOffer(offers, shown, "comeback", Now + 23 * Hour), "같은 팝업은 하루 한 번");
            Assert.AreEqual("offer_comeback", OfferRules.TryOffer(offers, shown, "comeback", Now + 25 * Hour));
            Assert.AreEqual(Now + 49 * Hour, offers["offer_comeback"], "끝난 뒤 다시 뜨면 24시간 새로");

            var list = new List<ProductDef>();
            OfferRules.ActiveOffers(offers, Now + 26 * Hour, list);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("offer_comeback", list[0].Id);
            Assert.AreEqual(23 * Hour, OfferRules.MsLeft(offers, "offer_comeback", Now + 26 * Hour));
        }

        [Test]
        public void TryOffer_StillActiveKeepsExpiry()
        {
            var offers = new Dictionary<string, long> { { "offer_arena", Now + 2 * Hour } };
            var shown = new Dictionary<string, long> { { "offer_arena", Now - 25 * Hour } };
            Assert.AreEqual("offer_arena", OfferRules.TryOffer(offers, shown, "arena", Now));
            Assert.AreEqual(Now + 2 * Hour, offers["offer_arena"], "떠 있는 동안은 남은 시간 그대로");
        }

        [Test]
        public void AfterMatch_PriorityArenaThenPackThenChapterThenComeback()
        {
            EconState e = Fresh();
            var offers = new Dictionary<string, long>();
            var shown = new Dictionary<string, long>();
            Assert.AreEqual("arena", OfferRules.AfterMatch(e, offers, shown, Now, Sid, true, true, true, out string id));
            Assert.AreEqual("offer_arena", id);
            Assert.AreEqual("chapter", OfferRules.AfterMatch(e, offers, shown, Now, Sid, true, true, true, out id), "승급 팝업은 오늘 이미 봄");
            Assert.AreEqual("comeback", OfferRules.AfterMatch(e, offers, shown, Now, Sid, true, true, true, out id));
            Assert.IsNull(OfferRules.AfterMatch(e, offers, shown, Now, Sid, true, true, true, out id));
            Assert.IsNull(id);
        }

        [Test]
        public void UnitPack_AppearsOneRoadStepBeforeTheUnit()
        {
            int at = TrophyRoad.UnitAt(Job.Ranger).Value;
            int before = OfferRules.UnitPackOpenAt(Job.Ranger).Value;
            Assert.Less(before, at);

            EconState e = Fresh();
            var shown = new Dictionary<string, long>();
            e.Pvp.Trophies = before - 1;
            Assert.IsNull(OfferRules.NextUnitPack(e, shown, Now, Sid));
            Assert.IsNull(OfferRules.UnitPackAvailable(e, shown, Job.Ranger, Sid), "아직 등장 전");
            e.Pvp.Trophies = before;
            Assert.AreEqual("unit_ranger", OfferRules.NextUnitPack(e, shown, Now, Sid));
            Assert.AreEqual("unit_ranger", OfferRules.UnitPackAvailable(e, shown, Job.Ranger, Sid));
            Assert.IsNull(OfferRules.NextUnitPack(e, shown, Now, Sid), "한 번만, 다음 패키지 지점 전");

            e.Pvp.Trophies = 0;
            Assert.AreEqual("unit_ranger", OfferRules.UnitPackAvailable(e, shown, Job.Ranger, Sid), "트로피가 내려가도 남는다");
            e.UnitLevels[Job.Ranger] = 5;
            Assert.IsNull(OfferRules.UnitPackAvailable(e, shown, Job.Ranger, Sid), "Lv5 이상이면 안 판다");
        }

        [Test]
        public void UnitPack_SkipsOwnedQuietly()
        {
            EconState e = Fresh();
            var shown = new Dictionary<string, long>();
            e.OneTime.Add("unit_ranger");
            e.Pvp.Trophies = OfferRules.UnitPackOpenAt(Job.Frost).Value;
            Assert.AreEqual("unit_frost", OfferRules.NextUnitPack(e, shown, Now, Sid));
            Assert.IsTrue(shown.ContainsKey("pack:unit_ranger"), "산 패키지는 조용히 등장 처리");
        }

        [Test]
        public void Shortage_SmallestGemProductThatCovers_OrUnitPack()
        {
            EconState e = Fresh();
            var shown = new Dictionary<string, long>();
            Assert.AreEqual("crystal_100", OfferRules.ShortageProduct(e, shown, 80, null, Sid));
            Assert.AreEqual("crystal_550", OfferRules.ShortageProduct(e, shown, 101, null, Sid));
            Assert.AreEqual("crystal_15000", OfferRules.ShortageProduct(e, shown, 99999, null, Sid), "모자라면 가장 큰 것");
            Assert.AreEqual("crystal_550", OfferRules.ShortageProduct(e, shown, 300, Job.Ranger, Sid), "등장 전 패키지는 안 보임");
            shown["pack:unit_ranger"] = Now;
            Assert.AreEqual("unit_ranger", OfferRules.ShortageProduct(e, shown, 300, Job.Ranger, Sid));
        }

        [Test]
        public void ProductOwned()
        {
            EconState e = Fresh();
            Assert.IsFalse(OfferRules.ProductOwned(e, "starter_pack", Sid));
            e.OneTime.Add("starter_pack");
            Assert.IsTrue(OfferRules.ProductOwned(e, "starter_pack", Sid));
            Assert.IsFalse(OfferRules.ProductOwned(e, "crystal_100", Sid), "여러 번 사는 상품");
            e.AdRemoved = true;
            Assert.IsTrue(OfferRules.ProductOwned(e, "ad_remove", Sid));
            e.Season.Premium = true;
            Assert.IsTrue(OfferRules.ProductOwned(e, "season_premium", Sid));
            Assert.IsFalse(OfferRules.ProductOwned(e, "season_premium", "S2026-12"), "지난 시즌 패스");
        }
    }
}
