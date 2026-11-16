using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>로비 표시 규칙 (chests.ts chestState·chestLeftMs·clockText·leftText, menu.ts 로드 바)</summary>
    public class LobbyRulesTests
    {
        static EconState Fresh() => EconState.Fresh("S2026-11");
        const long Now = 1_000_000_000L;

        [Test]
        public void ChestStatus_EmptyLockedOpeningReady()
        {
            EconState e = Fresh();
            e.Chests[0] = null;
            e.Chests[1] = new ChestSlot { Kind = ChestKind.Wood };
            e.Chests[2] = new ChestSlot { Kind = ChestKind.Silver, OpenAt = Now + 5000 };
            e.Chests[3] = new ChestSlot { Kind = ChestKind.Gold, OpenAt = Now };
            Assert.AreEqual(ChestStatus.Empty, LobbyRules.Status(e, 0, Now));
            Assert.AreEqual(ChestStatus.Locked, LobbyRules.Status(e, 1, Now));
            Assert.AreEqual(ChestStatus.Opening, LobbyRules.Status(e, 2, Now));
            Assert.AreEqual(ChestStatus.Ready, LobbyRules.Status(e, 3, Now), "openAt <= now 이면 열 수 있음");
            Assert.AreEqual(2, LobbyRules.OpeningSlot(e), "openAt 이 있는 첫 칸");
        }

        [Test]
        public void ChestLeft_LockedIsFullTime()
        {
            EconState e = Fresh();
            e.Chests[0] = new ChestSlot { Kind = ChestKind.Silver }; // 3시간
            e.Chests[1] = new ChestSlot { Kind = ChestKind.Wood, OpenAt = Now + 61_000 };
            e.Chests[2] = new ChestSlot { Kind = ChestKind.Wood, OpenAt = Now - 10 };
            e.Chests[3] = null;
            Assert.AreEqual(3 * 3600_000L, LobbyRules.LeftMs(e, 0, Now));
            Assert.AreEqual(61_000L, LobbyRules.LeftMs(e, 1, Now));
            Assert.AreEqual(0L, LobbyRules.LeftMs(e, 2, Now));
            Assert.AreEqual(0L, LobbyRules.LeftMs(e, 3, Now));
            Assert.AreEqual(-1, LobbyRules.OpeningSlot(Fresh()));
        }

        [Test]
        public void ClockText_HoursOnlyWhenNeeded()
        {
            Assert.AreEqual("01:01", LobbyRules.ClockText(60_001)); // 올림
            Assert.AreEqual("1:00:05", LobbyRules.ClockText(3605_000));
            Assert.AreEqual("00:00", LobbyRules.ClockText(-5));
        }

        [Test]
        public void LeftText_HoursAndMinutes()
        {
            Assert.AreEqual("30분", LobbyRules.LeftText(30 * 60_000));
            Assert.AreEqual("3시간", LobbyRules.LeftText(3 * 3600_000));
            Assert.AreEqual("2시간 5분", LobbyRules.LeftText((2 * 60 + 5) * 60_000));
        }

        [Test]
        public void RoadBar_FromPreviousStepToNext()
        {
            EconState e = Fresh();
            e.Pvp.Trophies = 0;
            Assert.AreEqual(0, LobbyRules.RoadPercent(e), 1e-9);
            e.Pvp.Trophies = 20; // 15 지남 → 다음 30: (20-15)/(30-15)
            e.RoadClaimed.Add(15);
            Assert.AreEqual(100.0 / 3, LobbyRules.RoadPercent(e), 1e-9);
            e.Pvp.Trophies = 5000;
            foreach (RoadStep s in TrophyRoad.Steps) if (!e.RoadClaimed.Contains(s.At)) e.RoadClaimed.Add(s.At);
            Assert.AreEqual(100, LobbyRules.RoadPercent(e), 1e-9, "다 받으면 가득");
        }

        [Test]
        public void RoadUnclaimed_ReachedButNotClaimed()
        {
            EconState e = Fresh();
            e.Pvp.Trophies = 40;
            Assert.IsTrue(LobbyRules.RoadUnclaimed(e));
            e.RoadClaimed.Add(15);
            e.RoadClaimed.Add(30);
            Assert.IsFalse(LobbyRules.RoadUnclaimed(e));
        }

        [Test]
        public void FeatureNew_UnlockedAndNotSeen()
        {
            EconState e = Fresh();
            var seen = new System.Collections.Generic.List<string>();
            Assert.IsFalse(LobbyRules.FeatureNew(e, seen, "units"));
            e.Stages["1-1"] = 1;
            Assert.IsTrue(LobbyRules.FeatureNew(e, seen, "units"));
            seen.Add("units");
            Assert.IsFalse(LobbyRules.FeatureNew(e, seen, "units"));
        }

        [Test]
        public void UnitsDotKey_ListsUpgradeableUnits()
        {
            EconState e = Fresh();
            Assert.IsNull(LobbyRules.UnitsDotKey(e));
            e.Cards[Job.Archer] = 100;
            e.Crystals = 10_000;
            Assert.AreEqual("units:Archer1", LobbyRules.UnitsDotKey(e));
        }
    }
}
