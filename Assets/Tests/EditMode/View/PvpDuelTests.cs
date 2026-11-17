using System;
using System.Collections;
using System.Threading.Tasks;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DefenseBattle.View.Tests
{
    /// <summary>대전 연결 (main.ts startPvp): 카운트다운, 결과 한 번만, 패배 뒤 6초 안전장치, 나가기</summary>
    public class PvpDuelTests
    {
        sealed class FakeOpponent : IPvpOpponent
        {
            public string Nick => "봇";
            public int Trophies => 0;
            public uint Seed => 1;
            public double StartDelay { get; set; } = PvpDuel.CountdownSec;
            public event Action<AttackPayload> Attack;
            public event Action<BoardSnapshot> Snapshot;
            public event Action<PvpOutcome> Finished;
            public int Defeats, Leaves, Ticks;
            public Task Begin() => Task.CompletedTask;
            public void Tick(double realDt) => Ticks++;
            public void SendAttack(AttackPayload p) { }
            public void SendSnapshot(BoardSnapshot s) { }
            public void ReportDefeat() => Defeats++;
            public void Leave() => Leaves++;
            public void Finish(bool won) => Finished?.Invoke(new PvpOutcome { Won = won, Delta = won ? 30 : -10 });
            public void Unused() { Attack?.Invoke(default); Snapshot?.Invoke(null); }
        }

        static Game NewGame() => new Game(new GameOptions { Mode = GameMode.Pvp, RandomSeed = 1 });

        [Test]
        public void Countdown_PausesUntilThreeSeconds()
        {
            Game g = NewGame();
            var opp = new FakeOpponent();
            var duel = new PvpDuel(g, opp);
            int go = 0;
            duel.Go += () => go++;
            duel.Tick(2.9);
            Assert.IsTrue(g.Paused);
            Assert.IsTrue(duel.CountingDown);
            Assert.AreEqual(0, opp.Ticks, "상대도 카운트다운 동안 멈춤");
            duel.Tick(0.2);
            Assert.IsFalse(g.Paused);
            Assert.AreEqual(1, go);
            duel.Tick(0.1);
            Assert.AreEqual(1, opp.Ticks);
        }

        [Test]
        public void Countdown_UsesOpponentStartDelay()
        {
            // 실시간 대전은 서버가 정한 시작 시각까지 남은 시간만큼 센다 (두 기기가 같은 순간에 시작)
            Game g = NewGame();
            var opp = new FakeOpponent { StartDelay = 1.2 };
            var duel = new PvpDuel(g, opp);
            Assert.AreEqual(1.2, duel.CountdownLeft, 1e-9);
            duel.Tick(1.1);
            Assert.IsTrue(g.Paused);
            duel.Tick(0.2);
            Assert.IsFalse(g.Paused);
        }

        [Test]
        public void Countdown_FollowsOpponentClock()
        {
            // 화면이 한 프레임 끊겨 deltaTime 이 잘려도, 실시간 상대가 가진 시계(서버 시작 시각)에 맞춰 시작한다
            Game g = NewGame();
            var opp = new FakeOpponent { StartDelay = 3 };
            var duel = new PvpDuel(g, opp);
            duel.Tick(0.1);
            opp.StartDelay = 0;
            duel.Tick(0.016);
            Assert.IsFalse(g.Paused);
        }

        [Test]
        public void Finished_Twice_ResultOnce()
        {
            var opp = new FakeOpponent();
            var duel = new PvpDuel(NewGame(), opp);
            int results = 0;
            duel.Result += _ => results++;
            opp.Finish(true);
            opp.Finish(true);
            Assert.AreEqual(1, results);
        }

        [Test]
        public void MyLoss_NoReply_FallbackAfterSixSeconds_ThenLateReplyIgnored()
        {
            Game g = NewGame();
            var opp = new FakeOpponent();
            var duel = new PvpDuel(g, opp);
            PvpOutcome got = null;
            int results = 0;
            duel.Result += o => { got = o; results++; };
            duel.Tick(PvpDuel.CountdownSec + 0.1);
            g.PvpEnd(false, "test"); // 내 전장이 무너짐
            Assert.AreEqual(1, opp.Defeats);
            duel.Tick(PvpDuel.ResultTimeout - 0.5);
            Assert.AreEqual(0, results, "아직 기다리는 중");
            duel.Tick(1);
            Assert.AreEqual(1, results);
            Assert.IsFalse(got.Won);
            Assert.AreEqual(0, got.Delta, "서버 결과가 없으면 트로피 변화 없음");
            opp.Finish(true);
            Assert.AreEqual(1, results, "늦게 온 결과는 무시");
        }

        [Test]
        public void WaitingForResult_KeepsTickingOpponent()
        {
            // 실시간 상대는 내가 무너진 뒤에도 Ping 으로 서버 결과를 받아야 한다
            Game g = NewGame();
            var opp = new FakeOpponent();
            var duel = new PvpDuel(g, opp);
            duel.Tick(PvpDuel.CountdownSec + 0.1);
            g.PvpEnd(false, "test");
            int before = opp.Ticks;
            duel.Tick(1);
            duel.Tick(1);
            Assert.AreEqual(before + 2, opp.Ticks);
        }

        [Test]
        public void Leave_ReportsDefeatOnce()
        {
            var opp = new FakeOpponent();
            var duel = new PvpDuel(NewGame(), opp);
            int results = 0;
            duel.Result += _ => results++;
            duel.Leave(true);
            duel.Leave(true);
            Assert.AreEqual(1, opp.Defeats);
            Assert.GreaterOrEqual(opp.Leaves, 1);
            opp.Finish(false);
            Assert.AreEqual(0, results, "나간 뒤엔 결과 화면 없음");
        }

        [UnityTest]
        public IEnumerator BotOpponent_ResultHangs_FinishesAfterTimeoutWithoutTrophies()
        {
            var bot = PvpRules.MakeBot(EconState.Fresh("S2026-11"), 6u, new SystemRandom());
            var never = new TaskCompletionSource<BotResultReply>();
            var opp = new BotOpponent(bot, "봇", 0, () => Task.FromResult("t1"), (id, won) => never.Task) { ReportTimeout = 0.2f };
            PvpOutcome got = null;
            opp.Finished += o => got = o;
            _ = opp.Begin();
            opp.ReportDefeat();
            float until = UnityEngine.Time.realtimeSinceStartup + 3f;
            while (got == null && UnityEngine.Time.realtimeSinceStartup < until) yield return null;
            Assert.IsNotNull(got, "서버 결과가 멈춰도 결과 화면은 뜬다");
            Assert.AreEqual(0, got.Delta);
        }

        [UnityTest]
        public IEnumerator BotOpponent_DefeatBeforeStartRecord_WaitsForTicket()
        {
            var ticket = new TaskCompletionSource<string>();
            string reportedId = null;
            bool? reportedWon = null;
            var bot = PvpRules.MakeBot(EconState.Fresh("S2026-11"), 5u, new SystemRandom());
            var opp = new BotOpponent(bot, "봇", 0, () => ticket.Task, (id, won) =>
            {
                reportedId = id;
                reportedWon = won;
                return Task.FromResult<BotResultReply>(null);
            });
            int finished = 0;
            opp.Finished += _ => finished++;
            _ = opp.Begin();
            opp.ReportDefeat(); // 시작 기록이 오기 전에 나감 (느린 연결)
            yield return null;
            Assert.IsNull(reportedId, "기록 id 를 받기 전엔 보고하지 않는다");
            ticket.SetResult("t1");
            for (int i = 0; i < 5 && finished == 0; i++) yield return null;
            Assert.AreEqual("t1", reportedId, "id 를 받은 뒤 패배를 보고한다");
            Assert.AreEqual(false, reportedWon);
            Assert.AreEqual(1, finished);
        }
    }
}
