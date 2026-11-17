using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine.TestTools;
using FakeServer = DefenseBattle.View.Tests.OnlineOpponentTests.FakeServer;
using FakeLink = DefenseBattle.View.Tests.OnlineOpponentTests.FakeLink;

namespace DefenseBattle.View.Tests
{
    /// <summary>실시간 상대 찾기·연결 (pvp.ts findOnlineMatch · OnlineSession.ready)</summary>
    public class MatchSearchTests
    {
        sealed class FakeFactory : IPvpLinkFactory
        {
            public readonly FakeLink Link = new FakeLink();
            public readonly List<string> Calls = new List<string>();
            public bool Fail;
            public Task<(IPvpLink link, string joinCode)> Host()
            {
                Calls.Add("host");
                if (Fail) return Task.FromException<(IPvpLink, string)>(new Exception("relay down"));
                return Task.FromResult<(IPvpLink, string)>((Link, "CODE1"));
            }
            public Task<IPvpLink> Join(string code)
            {
                Calls.Add("join:" + code);
                if (Fail) return Task.FromException<IPvpLink>(new Exception("relay down"));
                return Task.FromResult<IPvpLink>(Link);
            }
        }

        double m_now;

        [SetUp]
        public void ResetClock() => m_now = 0;
        MatchSearch New(FakeServer s, FakeFactory f) => new MatchSearch(s, f, () => m_now, Wait);

        /// <summary>가짜 시계를 넘기고 한 프레임 양보 (취소 플래그가 바뀔 틈)</summary>
        async Task Wait(double sec)
        {
            m_now += sec;
            await Task.Yield();
        }

        static MatchInfo Info(bool host) => new MatchInfo
        {
            MatchId = "m1", Seed = 5, Host = host, Opponent = new OpponentInfo { PlayerId = "opp", Nick = "상대", Trophies = 50 },
        };

        static QueueStatus Matched(bool host) => new QueueStatus { Status = QueueStatus.Matched, Match = Info(host) };

        static IEnumerator Until(Task t)
        {
            // Task.Yield 이어 하기는 에디터 갱신마다 돌아 프레임 수가 아니라 실제 시간으로 기다린다
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!t.IsCompleted && sw.ElapsedMilliseconds < 20000) yield return null;
            Assert.IsTrue(t.IsCompleted, "끝나야 한다");
            if (t.IsFaulted) throw t.Exception;
        }

        [UnityTest]
        public IEnumerator Find_MatchedOnQueue()
        {
            var s = new FakeServer();
            s.QueueReplies.Enqueue(Matched(true));
            Task<MatchInfo> t = New(s, new FakeFactory()).Find(8, () => false, _ => { });
            yield return Until(t);
            Assert.AreEqual("m1", t.Result.MatchId);
            CollectionAssert.AreEqual(new[] { "queue" }, s.Calls);
        }

        [UnityTest]
        public IEnumerator Find_MatchedOnPoll()
        {
            var s = new FakeServer();
            s.PollReplies.Enqueue(new QueueStatus { Status = QueueStatus.Waiting });
            s.PollReplies.Enqueue(Matched(false));
            Task<MatchInfo> t = New(s, new FakeFactory()).Find(8, () => false, _ => { });
            yield return Until(t);
            Assert.IsNotNull(t.Result);
            Assert.IsFalse(t.Result.Host);
            CollectionAssert.AreEqual(new[] { "queue", "poll", "poll" }, s.Calls);
        }

        [UnityTest]
        public IEnumerator Find_IdleRequeues()
        {
            var s = new FakeServer();
            s.PollReplies.Enqueue(new QueueStatus { Status = QueueStatus.Idle });
            s.QueueReplies.Enqueue(new QueueStatus { Status = QueueStatus.Waiting });
            s.QueueReplies.Enqueue(Matched(true));
            Task<MatchInfo> t = New(s, new FakeFactory()).Find(8, () => false, _ => { });
            yield return Until(t);
            Assert.IsNotNull(t.Result);
            CollectionAssert.AreEqual(new[] { "queue", "poll", "queue" }, s.Calls);
        }

        [UnityTest]
        public IEnumerator Find_Timeout_CancelsAndReturnsNull()
        {
            var s = new FakeServer();
            var seconds = new List<double>();
            Task<MatchInfo> t = New(s, new FakeFactory()).Find(8, () => false, sec => seconds.Add(sec));
            yield return Until(t);
            Assert.IsNull(t.Result, "8초 동안 없으면 봇");
            Assert.AreEqual("cancel", s.Calls[s.Calls.Count - 1]);
            Assert.Greater(seconds.Count, 10);
            Assert.LessOrEqual(m_now, 9);
        }

        [UnityTest]
        public IEnumerator Find_Cancelled_CancelsServer()
        {
            var s = new FakeServer();
            bool cancel = false;
            Task<MatchInfo> t = New(s, new FakeFactory()).Find(8, () => cancel, sec => { if (sec >= 1) cancel = true; });
            yield return Until(t);
            Assert.IsNull(t.Result);
            Assert.Contains("cancel", s.Calls);
            Assert.Less(m_now, 3);
        }

        [UnityTest]
        public IEnumerator Find_ServerError_ReturnsNull()
        {
            var t = new MatchSearch(new BrokenServer(), new FakeFactory(), () => m_now, Wait)
                .Find(8, () => false, _ => { });
            yield return Until(t);
            Assert.IsNull(t.Result, "서버에 못 닿으면 봇으로");
        }

        [UnityTest]
        public IEnumerator Connect_Host_UploadsCodeAndStarts()
        {
            var s = new FakeServer
            {
                ReadyReply = (m, c) => new MatchState { Status = MatchState.Waiting, RelayJoinCode = c },
            };
            s.PingReplies.Enqueue(new MatchState { Status = MatchState.Waiting, RelayJoinCode = "CODE1" });
            s.PingReplies.Enqueue(new MatchState { Status = MatchState.Playing, RelayJoinCode = "CODE1", StartAt = 10_000, Now = 6_500 });
            var f = new FakeFactory();
            Task<OnlineOpponent> t = New(s, f).Connect(Info(true), () => false);
            yield return Until(t);
            Assert.IsNotNull(t.Result);
            CollectionAssert.AreEqual(new[] { "host" }, f.Calls);
            Assert.AreEqual("ready:CODE1", s.Calls[0]);
            Assert.AreEqual(3.5, t.Result.StartDelay, 1e-6, "서버 시각 기준 남은 시간");
            Assert.AreEqual("m1", t.Result.MatchId);
        }

        [UnityTest]
        public IEnumerator Connect_Guest_JoinsRelayThenReadies()
        {
            // 손님은 Relay 에 들어간 뒤에야 준비한다 - 준비하는 순간 서버가 경기를 시작하므로, 그 전에 실패하면 취소(패배 아님)
            var s = new FakeServer { ReadyReply = (m, c) => new MatchState { Status = MatchState.Playing, RelayJoinCode = "CODE9", StartAt = 5_000, Now = 2_000 } };
            s.PingReplies.Enqueue(new MatchState { Status = MatchState.Waiting });
            s.PingReplies.Enqueue(new MatchState { Status = MatchState.Waiting, RelayJoinCode = "CODE9" });
            var f = new FakeFactory();
            Task<OnlineOpponent> t = New(s, f).Connect(Info(false), () => false);
            yield return Until(t);
            Assert.IsNotNull(t.Result);
            CollectionAssert.AreEqual(new[] { "ping", "ping", "ready:" }, s.Calls);
            CollectionAssert.AreEqual(new[] { "join:CODE9" }, f.Calls);
            Assert.AreEqual(3.0, t.Result.StartDelay, 1e-6);
        }

        [UnityTest]
        public IEnumerator Connect_GuestJoinFails_LeavesWithoutReady()
        {
            var s = new FakeServer { PingReply = new MatchState { Status = MatchState.Waiting, RelayJoinCode = "CODE9" } };
            var f = new FakeFactory { Fail = true };
            Task<OnlineOpponent> t = New(s, f).Connect(Info(false), () => false);
            yield return Until(t);
            Assert.IsNull(t.Result);
            Assert.IsFalse(s.Calls.Exists(c => c.StartsWith("ready")), "준비 전이면 서버가 경기를 취소한다 (트로피 변화 없음)");
            Assert.Contains("leave", s.Calls);
        }

        [UnityTest]
        public IEnumerator Connect_StartDelayCountsDownWithClock()
        {
            var s = new FakeServer { ReadyReply = (m, c) => new MatchState { Status = MatchState.Playing, RelayJoinCode = c, StartAt = 4_000, Now = 1_000 } };
            Task<OnlineOpponent> t = New(s, new FakeFactory()).Connect(Info(true), () => false);
            yield return Until(t);
            m_now += 1.25; // 결과를 받은 뒤 화면을 여는 데 걸린 시간
            Assert.AreEqual(1.75, t.Result.StartDelay, 1e-6);
            m_now += 5;
            Assert.AreEqual(0, t.Result.StartDelay);
        }

        [UnityTest]
        public IEnumerator Connect_Cancelled_ReturnsNullAndLeaves()
        {
            var s = new FakeServer();
            var f = new FakeFactory();
            bool cancel = false;
            Task<OnlineOpponent> t = New(s, f).Connect(Info(true), () => cancel);
            cancel = true;
            yield return Until(t);
            Assert.IsNull(t.Result);
            Assert.Less(m_now, 2, "취소하면 바로 그만둔다");
            Assert.Contains("leave", s.Calls);
            Assert.AreEqual(1, f.Link.Closes);
        }

        [UnityTest]
        public IEnumerator Connect_ReadyTimeout_ReturnsNull()
        {
            var s = new FakeServer { PingReply = new MatchState { Status = MatchState.Waiting } };
            var f = new FakeFactory();
            Task<OnlineOpponent> t = New(s, f).Connect(Info(true), () => false);
            yield return Until(t);
            Assert.IsNull(t.Result);
            Assert.GreaterOrEqual(m_now, MatchSearch.ReadyTimeout);
            Assert.Less(m_now, MatchSearch.ReadyTimeout + 2);
            Assert.Contains("leave", s.Calls);
            Assert.AreEqual(1, f.Link.Closes);
        }

        [UnityTest]
        public IEnumerator Connect_MatchCancelledByServer_ReturnsNull()
        {
            var s = new FakeServer { PingReply = new MatchState { Status = MatchState.Cancelled } };
            Task<OnlineOpponent> t = New(s, new FakeFactory()).Connect(Info(false), () => false);
            yield return Until(t);
            Assert.IsNull(t.Result);
        }

        [UnityTest]
        public IEnumerator Connect_RelayFails_ReturnsNullAndLeaves()
        {
            var s = new FakeServer();
            var f = new FakeFactory { Fail = true };
            Task<OnlineOpponent> t = New(s, f).Connect(Info(true), () => false);
            yield return Until(t);
            Assert.IsNull(t.Result);
            Assert.Contains("leave", s.Calls);
        }

        sealed class BrokenServer : IPvpServer
        {
            public string PlayerId => "me";
            static Task<T> Fail<T>() => Task.FromException<T>(new Exception("offline"));
            public Task<QueueStatus> Queue() => Fail<QueueStatus>();
            public Task<QueueStatus> Poll() => Fail<QueueStatus>();
            public Task Cancel() => Task.FromException(new Exception("offline"));
            public Task<MatchState> Ready(string m, string c) => Fail<MatchState>();
            public Task<MatchState> Ping(string m) => Fail<MatchState>();
            public Task<PvpMatchResult> Defeat(string m) => Fail<PvpMatchResult>();
            public Task<PvpMatchResult> Leave(string m) => Fail<PvpMatchResult>();
            public Task<PvpMatchResult> ClaimAway(string m) => Fail<PvpMatchResult>();
            public Task SyncEcon() => Task.CompletedTask;
        }
    }
}
