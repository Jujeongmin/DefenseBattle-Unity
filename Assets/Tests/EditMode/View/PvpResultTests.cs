using DefenseBattle.Core;
using NUnit.Framework;

namespace DefenseBattle.View.Tests
{
    /// <summary>대전 결과 화면 구성 (main.ts showPvpResult)</summary>
    public class PvpResultTests
    {
        static PvpOutcome Win(int crystals, ChestKind? chest) => new PvpOutcome
        {
            Won = true, Delta = 30, Trophies = 130,
            Reward = new PvpRewardResult { Crystals = crystals, Chest = chest },
        };

        static bool Has(ResultView v, ResultAction a)
        {
            foreach (ResultButton b in v.Buttons) if (b.Action == a) return true;
            return false;
        }

        [Test]
        public void Win_ShowsTrophyGemsChest()
        {
            ResultView v = ResultFlow.ForPvp(Win(20, ChestKind.Silver), "봇", 7, true);
            Assert.IsTrue(v.Win);
            Assert.AreEqual(Loc.Tl("{0} 승리!", Loc.Icon("trophy")), v.Title);
            StringAssert.Contains("봇", v.Sub);
            StringAssert.Contains("Wave 7", v.Sub);
            Assert.AreEqual(Loc.Icon("trophy") + " +30", v.Rewards[0]);
            Assert.AreEqual(Loc.Icon("gem") + " +20", v.Rewards[1]);
            StringAssert.Contains(Loc.Tl(Chests.Get(ChestKind.Silver).Kr), v.Rewards[2]);
            Assert.IsTrue(Has(v, ResultAction.Double));
        }

        [Test]
        public void ChestSlotsFull_ShowsFullLine()
        {
            ResultView v = ResultFlow.ForPvp(Win(20, null), "봇", 7, false);
            CollectionAssert.Contains(v.Rewards, Loc.Tl("{0} 상자 칸 가득", Loc.Icon("chest")));
            Assert.IsFalse(Has(v, ResultAction.Double), "광고가 없으면 2배 버튼 없음");
        }

        [Test]
        public void Loss_NoDoubleWithoutGems()
        {
            var o = new PvpOutcome { Won = false, Delta = -10, Trophies = 90, Reward = new PvpRewardResult { Crystals = 0 } };
            ResultView v = ResultFlow.ForPvp(o, "봇", 3, true);
            Assert.IsFalse(v.Win);
            Assert.AreEqual(Loc.Tl("패배"), v.Title);
            Assert.AreEqual(Loc.Icon("trophy") + " -10", v.Rewards[0]);
            Assert.IsFalse(Has(v, ResultAction.Double));
        }

        [Test]
        public void OpponentNick_IsNotParsedAsTags()
        {
            ResultView v = ResultFlow.ForPvp(Win(0, null), "<color=red>x</color>", 2, false);
            StringAssert.Contains("<noparse><color=red>x</color></noparse>", v.Sub, "실시간 상대 닉네임이 글자 태그로 먹히지 않게");
        }

        [Test]
        public void Buttons_RematchAndLobby()
        {
            ResultView v = ResultFlow.ForPvp(new PvpOutcome { Won = false }, "봇", 1, true);
            Assert.IsTrue(Has(v, ResultAction.Rematch));
            Assert.IsTrue(Has(v, ResultAction.Lobby));
            Assert.AreEqual(Loc.Icon("trophy") + " +0", v.Rewards[0], "서버 결과가 없으면 트로피 0");
        }
    }
}
