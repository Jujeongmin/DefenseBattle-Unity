using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>카드 공개 순서·시간 (chests.ts showReveal), 뽑기 비용, 확률 정보 표 (menu.ts openOdds)</summary>
    public class RevealAndOddsTests
    {
        static CardDrop D(Job j, int n = 1) => new CardDrop { Job = j, N = n };

        [Test]
        public void Reveal_LowestFirst_EpicWaitsLonger()
        {
            // 서버는 좋은 카드부터 준다 → 화면은 거꾸로 (가장 좋은 카드가 마지막)
            var drops = new List<CardDrop> { D(Job.Archmage), D(Job.Ranger), D(Job.Archer) };
            List<CardDrop> order = RevealRules.Order(drops);
            Assert.AreEqual(Job.Archer, order[0].Job);
            Assert.AreEqual(Job.Archmage, order[2].Job);
            int[] delays = RevealRules.Delays(order);
            CollectionAssert.AreEqual(new[] { 200, 400, 1020 }, delays, "일반·희귀 +200ms, 영웅 이상 +620ms");
            Assert.AreEqual(Units.Get(Job.Archmage).Rarity, RevealRules.Best(order));
            Assert.AreEqual(Rarity.Common, RevealRules.Best(new List<CardDrop>()));
        }

        [Test]
        public void GachaPlan_TicketsFirstElseGems()
        {
            Assert.AreEqual((true, 0), ShopRules.GachaPlan(10, 10), "뽑기권이 충분하면 뽑기권");
            Assert.AreEqual((false, Chests.GachaTen), ShopRules.GachaPlan(3, 10), "모자라면 보석 (서버는 섞어 쓰지 않는다)");
            Assert.AreEqual((true, 0), ShopRules.GachaPlan(1, 1));
            Assert.AreEqual((false, Chests.GachaOne), ShopRules.GachaPlan(0, 1));
        }

        [Test]
        public void Odds_GachaPerUnit()
        {
            int epicPool = 0;
            foreach (UnitDef u in Units.All) if (u.Rarity == Rarity.Epic) epicPool++;
            Assert.AreEqual(epicPool, ShopRules.PoolSize(Rarity.Epic));
            Assert.AreEqual("7%", ShopRules.Pct(0.07));
            Assert.AreEqual("0.125%", ShopRules.Pct(0.00125), "1% 미만은 소수 셋째 자리");
        }

        [Test]
        public void Odds_ChestRowGrowsWithArena()
        {
            ChestDef silver = Chests.Get(ChestKind.Silver); // 15장 +5/아레나, 희귀 3, 영웅 0.25
            ShopRules.ChestOdds r0 = ShopRules.ChestRow(silver, 0);
            Assert.AreEqual(15, r0.Total);
            Assert.AreEqual(3, r0.Rare);
            Assert.AreEqual(0, r0.EpicBase);
            Assert.AreEqual(0.25, r0.EpicChance, 1e-9);
            ShopRules.ChestOdds r2 = ShopRules.ChestRow(silver, 2);
            Assert.AreEqual(25, r2.Total);
            Assert.AreEqual(5, r2.Rare, "round(3 × 1.5)");
        }
    }
}
