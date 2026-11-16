using DefenseBattle.Core;
using NUnit.Framework;

namespace DefenseBattle.View.Tests
{
    /// <summary>판 끝 화면 (main.ts onGameOver / finishSolo / exitGame)</summary>
    public class ResultFlowTests
    {
        static readonly BattleLaunch Stage11 = new BattleLaunch(BattleLaunch.Mode.Stage, "1-1");
        static readonly BattleLaunch Endless = new BattleLaunch(BattleLaunch.Mode.Endless, null);

        static (Game, GameOverData) End(BattleLaunch l, bool win, int wave = 3, int peak = 20)
        {
            var g = new Game(l.MakeOptions());
            GameOverData data = null;
            g.GameEnded += d => data = d;
            for (int i = 0; i < wave; i++) g.StartNextWave();
            g.PeakMobs = peak;
            if (win) g.Win(); else g.GameOver("몹이 100마리를 넘었습니다");
            return (g, data);
        }

        [Test]
        public void Defeat_OffersReviveOnceWhenAdReady()
        {
            var (g, d) = End(Stage11, false);
            ResultView v = ResultFlow.ForGameOver(d, g, Stage11, true);
            Assert.AreEqual(Loc.Tl("위기!"), v.Title);
            Assert.AreEqual(d.Reason, v.Sub);
            Assert.AreEqual(ResultAction.Revive, v.Buttons[0].Action);
            Assert.AreEqual(ResultAction.GiveUp, v.Buttons[1].Action);

            Assert.IsTrue(g.Revive());
            g.GameOver("다시");
            ResultView again = ResultFlow.ForGameOver(new GameOverData { Wave = g.Wave, Reason = "다시", Cleared = false, PeakMobs = 20 }, g, Stage11, true);
            Assert.IsFalse(again.Buttons.Exists(b => b.Action == ResultAction.Revive), "부활은 판당 1회");
        }

        [Test]
        public void Defeat_NoAd_GoesStraightToResult()
        {
            var (g, d) = End(Stage11, false);
            ResultView v = ResultFlow.ForGameOver(d, g, Stage11, false);
            Assert.AreEqual(Loc.Tl("실패..."), v.Title);
        }

        [Test]
        public void StageClear_ShowsStars()
        {
            var (g, d) = End(Stage11, true, peak: 20);
            ResultView v = ResultFlow.Final(d, Stage11);
            Assert.IsTrue(v.Win);
            Assert.AreEqual(Loc.Tl("스테이지 클리어!"), v.Title);
            Assert.AreEqual(Campaign.StarsFor(20), v.Stars);
            Assert.AreEqual(Loc.Tl("{0} · 최대 필드 몹 {1}마리{2}", "1-1", 20, ""), v.Sub);
            Assert.AreEqual(ResultAction.Lobby, v.Buttons[v.Buttons.Count - 1].Action, "클리어면 다시 하기 없이 로비로");
            Assert.IsFalse(v.Buttons.Exists(b => b.Action == ResultAction.Retry));
        }

        [Test]
        public void StageFail_SubShowsWave()
        {
            var (g, d) = End(Stage11, false, wave: 4);
            ResultView v = ResultFlow.Final(d, Stage11);
            Assert.IsNull(v.Stars);
            Assert.AreEqual(d.Reason + " · Wave 4/" + Campaign.Get("1-1").Waves, v.Sub);
            Assert.AreEqual(ResultAction.Retry, v.Buttons[0].Action);
            Assert.AreEqual(ResultAction.Lobby, v.Buttons[1].Action);
        }

        [Test]
        public void Endless_GameOverTitle()
        {
            var (g, d) = End(Endless, false, wave: 7);
            ResultView v = ResultFlow.Final(d, Endless);
            Assert.AreEqual(Loc.Tl("게임 오버"), v.Title);
            Assert.AreEqual(Loc.Tl("{0} · 최고 기록 {1}웨이브", d.Reason, 7), v.Sub);
            CollectionAssert.Contains(v.Rewards, "Wave 7");
            var (g2, d2) = End(Endless, true, wave: 50);
            Assert.AreEqual(Loc.Tl("50웨이브 클리어!"), ResultFlow.Final(d2, Endless).Title);
        }

        [Test]
        public void ExitConfirm_AsksBeforeLeaving()
        {
            ResultView v = ResultFlow.ExitConfirm();
            Assert.AreEqual(Loc.Tl("나가기"), v.Title);
            Assert.AreEqual(ResultAction.Lobby, v.Buttons[0].Action);
            Assert.AreEqual(ResultAction.Close, v.Buttons[1].Action);
        }
    }
}
