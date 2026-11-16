using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>덱 화면 규칙 (progress.ts toggleDeck, menu.ts unitStatsHtml·deck-warn·unit 정렬)</summary>
    public class DeckRulesTests
    {
        static EconState Fresh() => EconState.Fresh("S2026-11");

        [Test]
        public void Toggle_AddsUntilFull_RemovesKeepsOne()
        {
            EconState e = Fresh(); // 궁수·마법사·전사
            e.UnitLevels[Job.Ranger] = 1;
            e.UnitLevels[Job.Frost] = 1;
            var deck = new List<Job>(e.Deck);
            Assert.IsTrue(DeckRules.Toggle(e, deck, Job.Ranger), "빈 칸이 있으면 넣는다");
            CollectionAssert.AreEqual(new[] { Job.Archer, Job.Wizard, Job.Warrior, Job.Ranger }, deck);
            Assert.IsFalse(DeckRules.Toggle(e, deck, Job.Frost), "가득 차면 못 넣는다");
            Assert.IsTrue(DeckRules.Toggle(e, deck, Job.Wizard), "덱에 있으면 뺀다");
            Assert.IsFalse(DeckRules.Toggle(e, deck, Job.Paladin), "없는 유닛은 못 넣는다");
            var one = new List<Job> { Job.Archer };
            Assert.IsFalse(DeckRules.Toggle(e, one, Job.Archer), "최소 1종");
        }

        [Test]
        public void UnitAttack_MatchesOriginalFormula()
        {
            // round(일반 등급 평균 공격 × atkMult × 희귀도 보너스 × (1 + (lv-1) × 0.08))
            UnitDef u = Units.Get(Job.Archer);
            Grade g = Grades.All[0];
            double baseAtk = (g.AtkMin + g.AtkMax) / 2;
            Assert.AreEqual((int)System.Math.Round(baseAtk * u.AtkMult * Units.RarityAtk[(int)u.Rarity] * (1 + 4 * Units.LevelAtk), System.MidpointRounding.AwayFromZero),
                DeckRules.UnitAttack(Job.Archer, 5));
        }

        [Test]
        public void GapCost_OnlyWhenDeckShort()
        {
            EconState e = Fresh(); // 3종 보유, 덱 3 → 빈 칸 없음 (DeckMin = 3)
            Assert.AreEqual(0, DeckRules.Gap(e, e.Deck));
            e.UnitLevels[Job.Ranger] = 1;  // 4종 보유, 덱 3 → 1칸 비었음
            Assert.AreEqual(1, DeckRules.Gap(e, e.Deck));
            Assert.AreEqual(30, DeckRules.GapSummonCost(1), "20 × (1 + 1 × 0.5)");
        }

        [Test]
        public void SortedUnits_OwnedFirstKeepingOrder()
        {
            EconState e = Fresh();
            e.UnitLevels[Job.Paladin] = 1;
            List<UnitDef> list = DeckRules.SortedUnits(e);
            Assert.AreEqual(Units.All.Length, list.Count);
            Assert.AreEqual(Job.Archer, list[0].Job);
            int owned = 0;
            while (owned < list.Count && e.Owns(list[owned].Job)) owned++;
            Assert.AreEqual(4, owned, "가진 유닛이 위로");
            Assert.IsTrue(list.IndexOf(Units.Get(Job.Paladin)) < 4);
        }

        [Test]
        public void SortedAttacks_UnlockedFirst()
        {
            EconState e = Fresh();
            List<AttackDef> list = DeckRules.SortedAttacks(e);
            for (int i = 0; i < Attacks.Starter.Length; i++) Assert.IsTrue(EconRules.AttackUnlocked(e, list[i].Key));
            Assert.IsFalse(EconRules.AttackUnlocked(e, list[Attacks.Starter.Length].Key));
        }

        [Test]
        public void PresetMissing_CountsLockedCards()
        {
            EconState e = Fresh();
            DeckPreset<Job> basic = Attacks.UnitPresets[0]; // 궁수·마법사·전사·레인저
            Assert.AreEqual(1, DeckRules.Missing(e, basic));
            e.UnitLevels[Job.Ranger] = 1;
            Assert.AreEqual(0, DeckRules.Missing(e, basic));
        }
    }
}
