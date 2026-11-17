using System;
using System.Threading.Tasks;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 실시간 상대 (pvp.ts OnlineSession). 전장 스냅샷·공격은 Relay(IPvpLink)로 상대 기기와 직접 주고받고,
    /// 승패·트로피·보상은 서버가 정한다 - 2초마다 Ping 으로 '아직 있음'을 알리며 결과를 확인하고,
    /// 상대 스냅이 20초 넘게 안 오면 이탈 승리를 요청한다 (서버가 상대의 마지막 응답 시각으로 다시 확인).
    /// 결과는 한 번만 알리고, 그 뒤 경제 상태를 다시 받는다.
    /// </summary>
    public sealed class OnlineOpponent : IPvpOpponent
    {
        /// <summary>서버에 '아직 있음'을 알리는 간격 (서버 AwayGraceMs 20초보다 충분히 짧게)</summary>
        public const double PingInterval = 2;
        /// <summary>상대 스냅이 이만큼 없으면 이탈 승리 요청 (pvp.ts oppGraceMs 20초 + 0.5초)</summary>
        public const double AwayGrace = 20.5;
        /// <summary>이탈 승리를 서버가 아직 안 받아 주면 다시 묻는 간격</summary>
        public const double ClaimRetry = 2;

        readonly MatchInfo m_match;
        readonly IPvpServer m_server;
        readonly IPvpLink m_link;
        readonly Func<double> m_clock;
        readonly double m_startDelay;
        readonly double m_startAt;
        readonly byte[] m_snapBuf = new byte[PvpWire.MaxSnapshotBytes];
        readonly byte[] m_atkBuf = new byte[64];
        readonly BoardSnapshot m_recv = new BoardSnapshot();
        double m_pingAcc;
        double m_silent;
        double m_claimWait;
        bool m_pinging;
        bool m_claiming;
        bool m_done;
        bool m_left;

        public string Nick => m_match.Opponent?.Nick ?? "";
        public int Trophies => m_match.Opponent?.Trophies ?? 0;
        public uint Seed => m_match.Seed;
        /// <summary>시작까지 남은 초 (시계가 있으면 지금 기준으로 줄어든다)</summary>
        public double StartDelay => m_clock == null ? m_startDelay : Math.Max(0, m_startAt - m_clock());
        public string MatchId => m_match.MatchId;

        public event Action<AttackPayload> Attack;
        public event Action<BoardSnapshot> Snapshot;
        public event Action<PvpOutcome> Finished;

        /// <param name="startDelay">시작까지 남은 초 (서버 시각 기준으로 계산한 값)</param>
        /// <param name="clock">실제 초 시계 - 주면 화면을 여는 동안 흐른 시간만큼 StartDelay 가 줄어든다</param>
        public OnlineOpponent(MatchInfo match, double startDelay, IPvpServer server, IPvpLink link, Func<double> clock = null)
        {
            m_match = match;
            m_startDelay = startDelay;
            m_clock = clock;
            m_startAt = clock != null ? clock() + startDelay : 0;
            m_server = server;
            m_link = link;
            link.SnapshotReceived += OnSnapshot;
            link.AttackReceived += OnAttack;
        }

        void OnSnapshot(byte[] buf, int len)
        {
            if (m_done || !PvpWire.ReadSnapshot(buf, len, m_recv)) return;
            m_silent = 0;
            Snapshot?.Invoke(m_recv);
        }

        void OnAttack(byte[] buf, int len)
        {
            if (!m_done && PvpWire.ReadAttack(buf, len, out AttackPayload p)) Attack?.Invoke(p);
        }

        /// <summary>준비·연결은 매칭(MatchSearch.Connect)에서 끝났다</summary>
        public Task Begin() => Task.CompletedTask;

        public void Tick(double realDt)
        {
            if (m_done || m_left) return;
            m_pingAcc += realDt;
            if (m_pingAcc >= PingInterval && !m_pinging)
            {
                m_pingAcc = 0;
                _ = PingAsync();
            }
            m_silent += realDt;
            if (m_claimWait > 0) m_claimWait -= realDt;
            if (m_silent >= AwayGrace && !m_claiming && m_claimWait <= 0) _ = ClaimAsync();
        }

        public void SendAttack(AttackPayload p)
        {
            if (!m_done) m_link.SendAttack(m_atkBuf, PvpWire.WriteAttack(p, m_atkBuf));
        }

        public void SendSnapshot(BoardSnapshot s)
        {
            if (!m_done) m_link.SendSnapshot(m_snapBuf, PvpWire.WriteSnapshot(s, m_snapBuf));
        }

        public void ReportDefeat() => _ = DefeatAsync();

        public void Leave()
        {
            if (m_left) return;
            m_left = true;
            if (!m_done) _ = LeaveAsync();
            m_link.Close();
        }

        async Task PingAsync()
        {
            m_pinging = true;
            try
            {
                MatchState st = await m_server.Ping(m_match.MatchId);
                if (st == null) return;
                if (st.Status == MatchState.Done && st.Result != null) Apply(st.Result);
                else if (st.Status == MatchState.Cancelled) Finish(new PvpOutcome { Won = false, Trophies = EconCache.State.Pvp.Trophies });
            }
            catch (Exception ex) { Debug.LogWarning("[Pvp] Ping 실패 (다음에 다시): " + ex.Message); }
            finally { m_pinging = false; }
        }

        async Task ClaimAsync()
        {
            m_claiming = true;
            try { Apply(await m_server.ClaimAway(m_match.MatchId)); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 이탈 승리 요청 실패: " + ex.Message); }
            finally
            {
                m_claiming = false;
                m_claimWait = ClaimRetry;
            }
        }

        async Task DefeatAsync()
        {
            try { Apply(await m_server.Defeat(m_match.MatchId)); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 패배 보고 실패 (Ping 으로 결과를 받는다): " + ex.Message); }
        }

        async Task LeaveAsync()
        {
            try { await m_server.Leave(m_match.MatchId); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 나가기 보고 실패: " + ex.Message); }
        }

        void Apply(PvpMatchResult r)
        {
            if (r == null || m_done) return;
            string me = m_server.PlayerId ?? "";
            // 서버는 경기를 먼저 'done' 으로 바꾸고 트로피·보상을 채운 뒤 결과를 다시 쓴다 - 내 트로피가 없으면 아직 반쪽, 다음 Ping 에서 받는다
            if (r.Trophies == null || !r.Trophies.ContainsKey(me)) return;
            var o = new PvpOutcome
            {
                Won = r.Winner == me,
                Delta = r.Delta != null && r.Delta.TryGetValue(me, out int d) ? d : 0,
                Trophies = r.Trophies != null && r.Trophies.TryGetValue(me, out int t) ? t : EconCache.State.Pvp.Trophies,
                Reward = r.Rewards != null && r.Rewards.TryGetValue(me, out PvpRewardResult rw) ? rw : null,
            };
            Finish(o);
            _ = SyncAsync();
        }

        void Finish(PvpOutcome o)
        {
            if (m_done) return;
            m_done = true;
            Finished?.Invoke(o);
        }

        async Task SyncAsync()
        {
            try { await m_server.SyncEcon(); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 결과 뒤 경제 동기화 실패: " + ex.Message); }
        }
    }
}
