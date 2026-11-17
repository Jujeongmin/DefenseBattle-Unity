using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DefenseBattle.View.Tests
{
    /// <summary>실시간 상대 (pvp.ts OnlineSession): Relay 로 전장을 주고받고, 결과는 서버가 정한다</summary>
    public class OnlineOpponentTests
    {
        internal sealed class FakeLink : IPvpLink
        {
            public bool Connected { get; set; } = true;
            public event Action<byte[], int> SnapshotReceived;
            public event Action<byte[], int> AttackReceived;
            public readonly List<byte[]> SentSnaps = new List<byte[]>();
            public readonly List<byte[]> SentAttacks = new List<byte[]>();
            public int Closes;
            public void SendSnapshot(byte[] b, int len) => SentSnaps.Add(Copy(b, len));
            public void SendAttack(byte[] b, int len) => SentAttacks.Add(Copy(b, len));
            public void Close() => Closes++;
            public void Snap(byte[] b) => SnapshotReceived?.Invoke(b, b.Length);
            public void Atk(byte[] b) => AttackReceived?.Invoke(b, b.Length);
            static byte[] Copy(byte[] b, int len) { var c = new byte[len]; Array.Copy(b, c, len); return c; }
        }

        internal sealed class FakeServer : IPvpServer
        {
            public string PlayerId => "me";
            public readonly List<string> Calls = new List<string>();
            public MatchState PingReply = new MatchState { Status = MatchState.Playing };
            public Queue<MatchState> PingReplies = new Queue<MatchState>();
            public PvpMatchResult DefeatReply;
            public PvpMatchResult ClaimReply;
            public PvpMatchResult LeaveReply;
            public Queue<QueueStatus> QueueReplies = new Queue<QueueStatus>();
            public Queue<QueueStatus> PollReplies = new Queue<QueueStatus>();
            public Func<string, string, MatchState> ReadyReply = (m, c) => new MatchState { Status = MatchState.Waiting };
            public int Syncs;

            public Task<QueueStatus> Queue() { Calls.Add("queue"); return Task.FromResult(QueueReplies.Count > 0 ? QueueReplies.Dequeue() : new QueueStatus { Status = QueueStatus.Waiting }); }
            public Task<QueueStatus> Poll() { Calls.Add("poll"); return Task.FromResult(PollReplies.Count > 0 ? PollReplies.Dequeue() : new QueueStatus { Status = QueueStatus.Waiting }); }
            public Task Cancel() { Calls.Add("cancel"); return Task.CompletedTask; }
            public Task<MatchState> Ready(string matchId, string joinCode) { Calls.Add("ready:" + joinCode); return Task.FromResult(ReadyReply(matchId, joinCode)); }
            public Task<MatchState> Ping(string matchId) { Calls.Add("ping"); return Task.FromResult(PingReplies.Count > 0 ? PingReplies.Dequeue() : PingReply); }
            public Task<PvpMatchResult> Defeat(string matchId) { Calls.Add("defeat"); return Task.FromResult(DefeatReply); }
            public Task<PvpMatchResult> Leave(string matchId) { Calls.Add("leave"); return Task.FromResult(LeaveReply); }
            public Task<PvpMatchResult> ClaimAway(string matchId) { Calls.Add("claim"); return Task.FromResult(ClaimReply); }
            public Task SyncEcon() { Syncs++; return Task.CompletedTask; }
        }

        static MatchInfo Match() => new MatchInfo
        {
            MatchId = "m1", Seed = 77, Host = true,
            Opponent = new OpponentInfo { PlayerId = "opp", Nick = "상대", Trophies = 120 },
        };

        static PvpMatchResult Result(string winner)
        {
            var r = new PvpMatchResult { MatchId = "m1", Winner = winner, Loser = winner == "me" ? "opp" : "me", Reason = "defeat" };
            r.Delta["me"] = winner == "me" ? 30 : -20;
            r.Trophies["me"] = winner == "me" ? 160 : 110;
            r.Rewards["me"] = new PvpRewardResult();
            return r;
        }

        static IEnumerator Frames(int n = 5)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [Test]
        public void ExposesMatchInfo()
        {
            var opp = new OnlineOpponent(Match(), 2.5, new FakeServer(), new FakeLink());
            Assert.AreEqual("상대", opp.Nick);
            Assert.AreEqual(120, opp.Trophies);
            Assert.AreEqual(77u, opp.Seed);
            Assert.AreEqual(2.5, opp.StartDelay);
        }

        [Test]
        public void Snapshot_FromLink_RaisesSnapshot()
        {
            var link = new FakeLink();
            var opp = new OnlineOpponent(Match(), 3, new FakeServer(), link);
            BoardSnapshot got = null;
            opp.Snapshot += s => got = s;
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            var sent = new BoardSnapshot { Wave = 9 };
            sent.Mobs.Add(4);
            int n = PvpWire.WriteSnapshot(sent, buf);
            var bytes = new byte[n];
            Array.Copy(buf, bytes, n);
            link.Snap(bytes);
            Assert.IsNotNull(got);
            Assert.AreEqual(9, got.Wave);
            CollectionAssert.AreEqual(new[] { 4 }, got.Mobs);
        }

        [Test]
        public void Attack_RoundTripsThroughLink()
        {
            var link = new FakeLink();
            var opp = new OnlineOpponent(Match(), 3, new FakeServer(), link);
            opp.SendAttack(new AttackPayload { Card = 5, Wave = 3, Pos = 0.25 });
            Assert.AreEqual(1, link.SentAttacks.Count);
            AttackPayload got = default;
            bool fired = false;
            opp.Attack += p => { got = p; fired = true; };
            link.Atk(link.SentAttacks[0]);
            Assert.IsTrue(fired);
            Assert.AreEqual(5, got.Card);
            Assert.AreEqual(0.25, got.Pos);
        }

        [Test]
        public void SendSnapshot_WritesToLink()
        {
            var link = new FakeLink();
            var opp = new OnlineOpponent(Match(), 3, new FakeServer(), link);
            opp.SendSnapshot(new BoardSnapshot { Wave = 4 });
            Assert.AreEqual(1, link.SentSnaps.Count);
            var back = new BoardSnapshot();
            Assert.IsTrue(PvpWire.ReadSnapshot(link.SentSnaps[0], link.SentSnaps[0].Length, back));
            Assert.AreEqual(4, back.Wave);
        }

        [UnityTest]
        public IEnumerator Ping_DoneResult_FinishesOnceAndSyncs()
        {
            var server = new FakeServer { PingReply = new MatchState { Status = MatchState.Done, Result = Result("me") } };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            var outs = new List<PvpOutcome>();
            opp.Finished += o => outs.Add(o);
            opp.Tick(OnlineOpponent.PingInterval);
            yield return Frames();
            opp.Tick(OnlineOpponent.PingInterval);
            yield return Frames();
            Assert.AreEqual(1, outs.Count);
            Assert.IsTrue(outs[0].Won);
            Assert.AreEqual(30, outs[0].Delta);
            Assert.AreEqual(160, outs[0].Trophies);
            Assert.IsNotNull(outs[0].Reward);
            Assert.AreEqual(1, server.Syncs, "결과 뒤 경제 상태를 다시 받는다");
        }

        [UnityTest]
        public IEnumerator Ping_ResultWithoutMyTrophiesYet_WaitsForFullResult()
        {
            // 서버는 경기를 'done' 으로 먼저 바꾸고 트로피·보상을 채운 뒤 결과를 다시 쓴다 - 그 사이 Ping 은 반쪽 결과를 받는다
            var partial = new PvpMatchResult { MatchId = "m1", Winner = "me", Loser = "opp", Reason = "leave" };
            var server = new FakeServer();
            server.PingReplies.Enqueue(new MatchState { Status = MatchState.Done, Result = partial });
            server.PingReply = new MatchState { Status = MatchState.Done, Result = Result("me") };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            var outs = new List<PvpOutcome>();
            opp.Finished += o => outs.Add(o);
            opp.Tick(OnlineOpponent.PingInterval);
            yield return Frames();
            Assert.AreEqual(0, outs.Count, "반쪽 결과로 끝내지 않는다");
            opp.Tick(OnlineOpponent.PingInterval);
            yield return Frames();
            Assert.AreEqual(1, outs.Count);
            Assert.AreEqual(30, outs[0].Delta);
            Assert.AreEqual(160, outs[0].Trophies);
        }

        [UnityTest]
        public IEnumerator Defeat_ResultFinishes()
        {
            var server = new FakeServer { DefeatReply = Result("opp") };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            PvpOutcome got = null;
            opp.Finished += o => got = o;
            opp.ReportDefeat();
            yield return Frames();
            Assert.IsNotNull(got);
            Assert.IsFalse(got.Won);
            Assert.AreEqual(-20, got.Delta);
            Assert.AreEqual(110, got.Trophies);
        }

        [UnityTest]
        public IEnumerator DefeatAndPingBothReturnResult_FinishedOnce()
        {
            var server = new FakeServer { DefeatReply = Result("opp"), PingReply = new MatchState { Status = MatchState.Done, Result = Result("opp") } };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            int n = 0;
            opp.Finished += o => n++;
            opp.ReportDefeat();
            opp.Tick(OnlineOpponent.PingInterval);
            yield return Frames();
            Assert.AreEqual(1, n);
        }

        [UnityTest]
        public IEnumerator NoSnapshotsForGrace_ClaimsAway()
        {
            var server = new FakeServer { ClaimReply = Result("me") };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            PvpOutcome got = null;
            opp.Finished += o => got = o;
            for (int i = 0; i < 21; i++) opp.Tick(1);
            yield return Frames();
            Assert.Contains("claim", server.Calls);
            Assert.IsNotNull(got);
            Assert.IsTrue(got.Won);
        }

        [UnityTest]
        public IEnumerator ClaimRefused_RetriesLater()
        {
            var server = new FakeServer { ClaimReply = null };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            for (int i = 0; i < 21; i++) opp.Tick(1);
            yield return Frames();
            int first = server.Calls.FindAll(c => c == "claim").Count;
            opp.Tick(0.5);
            yield return Frames();
            Assert.AreEqual(first, server.Calls.FindAll(c => c == "claim").Count, "바로 다시 요청하지 않는다");
            opp.Tick(OnlineOpponent.ClaimRetry);
            yield return Frames();
            Assert.Greater(server.Calls.FindAll(c => c == "claim").Count, first);
        }

        [UnityTest]
        public IEnumerator ClaimRefusedRepeatedly_BacksOff()
        {
            // 연결만 끊기고 상대는 서버에 계속 있으면 서버가 계속 거절한다 - 갈수록 드물게 묻는다
            var server = new FakeServer { ClaimReply = null };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            for (int i = 0; i < 21; i++) opp.Tick(1);
            yield return Frames();
            for (int i = 0; i < 60; i++)
            {
                opp.Tick(1);
                yield return null;
            }
            yield return Frames();
            int claims = server.Calls.FindAll(c => c == "claim").Count;
            Assert.LessOrEqual(claims, 8, "60초 동안 2초마다(30번) 묻지 않는다");
            Assert.GreaterOrEqual(claims, 3);
        }

        [UnityTest]
        public IEnumerator SnapshotsFlowing_DoesNotClaim()
        {
            var link = new FakeLink();
            var server = new FakeServer();
            var opp = new OnlineOpponent(Match(), 3, server, link);
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            int n = PvpWire.WriteSnapshot(new BoardSnapshot(), buf);
            var bytes = new byte[n];
            Array.Copy(buf, bytes, n);
            for (int i = 0; i < 40; i++)
            {
                opp.Tick(1);
                link.Snap(bytes);
            }
            yield return Frames();
            Assert.IsFalse(server.Calls.Contains("claim"));
        }

        [UnityTest]
        public IEnumerator Leave_CallsServerAndClosesLink()
        {
            var link = new FakeLink();
            var server = new FakeServer();
            var opp = new OnlineOpponent(Match(), 3, server, link);
            opp.Leave();
            yield return Frames();
            Assert.Contains("leave", server.Calls);
            Assert.AreEqual(1, link.Closes);
        }

        [UnityTest]
        public IEnumerator LeaveAfterResult_DoesNotCallServer()
        {
            var link = new FakeLink();
            var server = new FakeServer { DefeatReply = Result("opp") };
            var opp = new OnlineOpponent(Match(), 3, server, link);
            opp.ReportDefeat();
            yield return Frames();
            opp.Leave();
            yield return Frames();
            Assert.IsFalse(server.Calls.Contains("leave"));
            Assert.AreEqual(1, link.Closes);
        }

        [UnityTest]
        public IEnumerator Ping_Cancelled_FinishesAsLossWithoutTrophyChange()
        {
            var server = new FakeServer { PingReply = new MatchState { Status = MatchState.Cancelled } };
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            PvpOutcome got = null;
            opp.Finished += o => got = o;
            opp.Tick(OnlineOpponent.PingInterval);
            yield return Frames();
            Assert.IsNotNull(got);
            Assert.IsFalse(got.Won);
            Assert.AreEqual(0, got.Delta);
        }

        [UnityTest]
        public IEnumerator ServerErrors_DoNotThrowOrFinish()
        {
            var server = new ThrowingServer();
            var opp = new OnlineOpponent(Match(), 3, server, new FakeLink());
            bool finished = false;
            opp.Finished += o => finished = true;
            opp.Tick(OnlineOpponent.PingInterval);
            opp.ReportDefeat();
            yield return Frames();
            Assert.IsFalse(finished, "결과는 다음 Ping 에서 받는다");
        }

        sealed class ThrowingServer : IPvpServer
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
