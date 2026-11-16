using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>진행 규칙 (progress.ts): 스테이지 해금·다음 스테이지·챕터·기능 해금·덱 편성·판 옵션·트로피 로드</summary>
    public class ProgressTests
    {
        static EconState Fresh() => EconState.Fresh("S2026-11");

        [Test]
        public void Stages_UnlockInOrder()
        {
            EconState e = Fresh();
            Assert.IsTrue(Progress.StageUnlocked(e, "1-1"));
            Assert.IsFalse(Progress.StageUnlocked(e, "1-2"));
            Assert.AreEqual("1-1", Progress.NextStageId(e));
            e.Stages["1-1"] = 2;
            Assert.IsTrue(Progress.StageUnlocked(e, "1-2"));
            Assert.AreEqual("1-2", Progress.NextStageId(e));
            Assert.AreEqual(2, Progress.ChapterStars(e, 1));
        }

        [Test]
        public void NextStage_AllClearedIsLast()
        {
            EconState e = Fresh();
            foreach (StageDef s in Campaign.Stages) e.Stages[s.Id] = 3;
            Assert.AreEqual(Campaign.Stages[Campaign.Stages.Length - 1].Id, Progress.NextStageId(e));
        }

        [Test]
        public void Chapter2_NeedsLastStageOfChapter1()
        {
            EconState e = Fresh();
            Assert.IsTrue(Progress.ChapterUnlocked(e, 1));
            Assert.IsFalse(Progress.ChapterUnlocked(e, 2));
            e.Stages["1-" + Campaign.StagesPerChapter] = 1;
            Assert.IsTrue(Progress.ChapterUnlocked(e, 2));
        }

        [Test]
        public void Feature_UnlocksWhenStageCleared()
        {
            foreach (KeyValuePair<string, string> kv in Campaign.FeatureUnlock)
            {
                EconState e = Fresh(); // 같은 스테이지에서 여러 기능이 열리므로 기능마다 새 계정
                Assert.IsFalse(Progress.FeatureUnlocked(e, kv.Key), kv.Key);
                e.Stages[kv.Value] = 1;
                Assert.IsTrue(Progress.FeatureUnlocked(e, kv.Key), kv.Key);
            }
        }

        [Test]
        public void PlaceDeck_SwapsReplacesOrAppends()
        {
            EconState e = Fresh(); // 덱 = 궁수·마법사·전사
            e.UnitLevels[Job.Ranger] = 1;
            var deck = new List<Job>(e.Deck);
            Assert.IsTrue(Progress.PlaceDeck(e, deck, Job.Ranger, 3), "빈 칸 → 뒤에 붙임");
            CollectionAssert.AreEqual(new[] { Job.Archer, Job.Wizard, Job.Warrior, Job.Ranger }, deck);
            Assert.IsTrue(Progress.PlaceDeck(e, deck, Job.Ranger, 0), "이미 덱 → 두 칸 바꿈");
            CollectionAssert.AreEqual(new[] { Job.Ranger, Job.Wizard, Job.Warrior, Job.Archer }, deck);
            Assert.IsFalse(Progress.PlaceDeck(e, deck, Job.Frost, 0), "없는 유닛");
            Assert.IsTrue(Progress.RemoveDeck(deck, Job.Wizard));
            Assert.AreEqual(3, deck.Count);
            deck.Clear();
            deck.Add(Job.Archer);
            Assert.IsFalse(Progress.RemoveDeck(deck, Job.Archer), "최소 1");
        }

        [Test]
        public void StageOptions_UseMeta()
        {
            EconState e = Fresh();
            e.Research[ResearchKey.Atk] = 3;
            e.UnitLevels[Job.Archer] = 4;
            GameOptions o = Progress.StageOptions(e, "1-2", true);
            Assert.AreEqual("1-2", o.StageId);
            Assert.AreEqual(Campaign.Get("1-2").Waves, o.WaveCount);
            CollectionAssert.AreEqual(e.Deck, o.Deck);
            Assert.AreEqual(3, o.DeckMin, "보유 3종 → 3");
            Assert.AreEqual(4, o.UnitLevels[Job.Archer]);
            Assert.AreEqual(3, o.Research[ResearchKey.Atk]);
            Assert.AreEqual(2, o.StartGoldMult, "골드 부스터");
            GameOptions endless = Progress.EndlessOptions(e, false);
            Assert.IsNull(endless.StageId);
            Assert.AreEqual(1, endless.StartGoldMult);
        }

        [Test]
        public void NextRoadStep_SkipsReachedAndClaimed()
        {
            EconState e = Fresh();
            RoadStep first = TrophyRoad.Steps[0];
            Assert.AreSame(first, Progress.NextRoadStep(e));
            e.Pvp.Trophies = first.At;
            Assert.AreSame(TrophyRoad.Steps[1], Progress.NextRoadStep(e), "도달한 단계는 다음으로");
        }
    }
}
