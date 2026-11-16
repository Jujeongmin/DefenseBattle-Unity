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
            var g = new Game(l.MakeOptions(EconState.Fresh("S2026-11")));
            GameOverData data = null;
            g.GameEnded += d => data = d;
            for (int i = 0; i < wave; i++) g.StartNextWave();
            g.PeakMobs = peak;
            if (win) g.Win(); else g.GameOver("몹이 100마리를 넘었습니다");
            return (g, data);
        }

        static EconState Fresh() => EconState.Fresh("S2026-11");
        static readonly RunOutcome NoReward = new RunOutcome();

        [Test]
        public void Defeat_OffersReviveOnceWhenAdReady()
        {
            var (g, d) = End(Stage11, false);
            ResultView v = ResultFlow.ForGameOver(d, g, true);
            Assert.AreEqual(Loc.Tl("위기!"), v.Title);
            Assert.AreEqual(d.Reason, v.Sub);
            Assert.AreEqual(ResultAction.Revive, v.Buttons[0].Action);
            Assert.AreEqual(ResultAction.GiveUp, v.Buttons[1].Action);

            Assert.IsTrue(g.Revive());
            g.GameOver("다시");
            ResultView again = ResultFlow.ForGameOver(new GameOverData { Wave = g.Wave, Reason = "다시", Cleared = false, PeakMobs = 20 }, g, true);
            Assert.IsNull(again, "부활은 판당 1회 - 바로 최종 결과로");
        }

        [Test]
        public void Defeat_NoAd_GoesStraightToResult()
        {
            var (g, d) = End(Stage11, false);
            Assert.IsNull(ResultFlow.ForGameOver(d, g, false), "광고가 없으면 위기 창 없이 최종 결과");
        }

        [Test]
        public void StageClear_ServerRewardNextStageAndDouble()
        {
            var (g, d) = End(Stage11, true, peak: 20);
            EconState e = Fresh();
            e.Stages["1-1"] = 3; // 서버가 반영한 뒤의 상태
            var o = new RunOutcome { Stage = new StageEndResult { Stars = 3, FirstClear = true, Crystals = 40, Unlocked = Job.Ranger }, AdReady = true };
            ResultView v = ResultFlow.Final(d, Stage11, o, e);
            Assert.AreEqual(Loc.Tl("{0} · 최대 필드 몹 {1}마리{2}", "1-1", 20, Loc.Tl(" · 첫 클리어!")), v.Sub);
            CollectionAssert.Contains(v.Rewards, Loc.Icon("gem") + " +40");
            StringAssert.Contains(Loc.Tl(Units.Get(Job.Ranger).Kr), v.Extra);
            Assert.AreEqual(ResultAction.Double, v.Buttons[0].Action);
            Assert.AreEqual(Loc.Tl("{0} 광고 보고 보상 2배 ({1}+{2})", Loc.Icon("tv"), Loc.Icon("gem"), 40), v.Buttons[0].Label);
            Assert.AreEqual(ResultAction.Next, v.Buttons[1].Action);
            Assert.AreEqual("1-2", v.Buttons[1].StageId);
            Assert.AreEqual(ResultAction.Lobby, v.Buttons[2].Action);
        }

        [Test]
        public void StageClear_NoRewardWhenServerRejected_NoDouble()
        {
            var (g, d) = End(Stage11, true, peak: 20);
            ResultView v = ResultFlow.Final(d, Stage11, new RunOutcome { AdReady = true }, Fresh());
            Assert.AreEqual(0, v.Rewards.Count);
            Assert.IsFalse(v.Buttons.Exists(b => b.Action == ResultAction.Double));
            Assert.IsFalse(v.Buttons.Exists(b => b.Action == ResultAction.Next), "서버 반영 전이면 다음 스테이지가 아직 잠김");
        }

        [Test]
        public void TutorialFail_OnlyRetryDirectly()
        {
            var (g, d) = End(Stage11, false);
            ResultView v = ResultFlow.Final(d, Stage11, new RunOutcome { TutorialRun = true }, Fresh());
            Assert.AreEqual(1, v.Buttons.Count, "튜토리얼 판에서 지면 로비로 없이 다시 하기 하나");
            Assert.AreEqual(ResultAction.Retry, v.Buttons[0].Action);
        }

        [Test]
        public void StageClear_ShowsStars()
        {
            var (g, d) = End(Stage11, true, peak: 20);
            ResultView v = ResultFlow.Final(d, Stage11, NoReward, Fresh());
            Assert.IsTrue(v.Win);
            Assert.AreEqual(Loc.Tl("스테이지 클리어!"), v.Title);
            Assert.AreEqual(Campaign.StarsFor(20), v.Stars);
            Assert.AreEqual(Loc.Tl("{0} · 최대 필드 몹 {1}마리{2}", "1-1", 20, ""), v.Sub);
            Assert.AreEqual(ResultAction.Lobby, v.Buttons[v.Buttons.Count - 1].Action, "클리어면 다시 하기 없이 로비로");
            Assert.IsFalse(v.Buttons.Exists(b => b.Action == ResultAction.Retry || b.Action == ResultAction.StageInfo));
        }

        [Test]
        public void StageFail_SubShowsWave()
        {
            var (g, d) = End(Stage11, false, wave: 4);
            ResultView v = ResultFlow.Final(d, Stage11, NoReward, Fresh());
            Assert.IsNull(v.Stars);
            Assert.AreEqual(d.Reason + " · Wave 4/" + Campaign.Get("1-1").Waves, v.Sub);
            Assert.AreEqual(ResultAction.StageInfo, v.Buttons[0].Action, "다시 하기 = 스테이지 정보 창 (덱·부스터 다시 고름)");
            Assert.AreEqual("1-1", v.Buttons[0].StageId);
            Assert.AreEqual(ResultAction.Lobby, v.Buttons[1].Action);
        }

        [Test]
        public void Endless_GameOverTitle()
        {
            var (g, d) = End(Endless, false, wave: 7);
            ResultView v = ResultFlow.Final(d, Endless, new RunOutcome { BestWave = 7 }, Fresh());
            Assert.AreEqual(Loc.Tl("게임 오버"), v.Title);
            Assert.AreEqual(Loc.Tl("{0} · 최고 기록 {1}웨이브", d.Reason, 7), v.Sub);
            CollectionAssert.Contains(v.Rewards, "Wave 7");
            CollectionAssert.Contains(v.Rewards, Loc.Icon("gem") + " +0");
            Assert.AreEqual(ResultAction.Retry, v.Buttons[0].Action);
            var (g2, d2) = End(Endless, true, wave: 50);
            Assert.AreEqual(Loc.Tl("50웨이브 클리어!"), ResultFlow.Final(d2, Endless, new RunOutcome { BestWave = 50 }, Fresh()).Title);
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
