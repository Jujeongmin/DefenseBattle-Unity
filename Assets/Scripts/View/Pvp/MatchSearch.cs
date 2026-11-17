using System;
using System.Threading.Tasks;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 실시간 상대 찾기와 연결 (pvp.ts findOnlineMatch · OnlineSession.ready).
    /// Find: 대기열에 넣고 0.5초마다 확인 (대기열이 만료됐으면 다시 등록), maxWait 초 안에 못 찾거나 서버에 못 닿으면 null → 봇.
    /// Connect: 호스트는 Relay 방을 열어 참가 코드와 함께 준비, 손님은 준비 뒤 코드가 오면 들어간다.
    /// 둘 다 준비돼 서버가 시작 시각을 정하면 OnlineOpponent 를 돌려준다. 취소·30초 초과·실패면 경기에서 나가고 null.
    /// </summary>
    public sealed class MatchSearch
    {
        /// <summary>확인 간격 (pvp.ts 0.5초)</summary>
        public const double PollInterval = 0.5;
        /// <summary>매칭 뒤 시작까지 기다리는 최대 초 (서버 ReadyTimeoutMs)</summary>
        public const double ReadyTimeout = 30;

        readonly IPvpServer m_server;
        readonly IPvpLinkFactory m_links;
        readonly Func<double> m_clock;
        readonly Func<double, Task> m_wait;

        /// <param name="clock">실제 초 시계</param>
        /// <param name="wait">초만큼 기다리기</param>
        public MatchSearch(IPvpServer server, IPvpLinkFactory links, Func<double> clock, Func<double, Task> wait)
        {
            m_server = server;
            m_links = links;
            m_clock = clock;
            m_wait = wait;
        }

        /// <param name="onSecond">기다린 초 (찾는 중 표시)</param>
        public async Task<MatchInfo> Find(double maxWait, Func<bool> cancelled, Action<double> onSecond)
        {
            try
            {
                QueueStatus q = await m_server.Queue();
                if (Matched(q)) return q.Match;
                double t0 = m_clock();
                while (true)
                {
                    double sec = m_clock() - t0;
                    onSecond?.Invoke(sec);
                    if (cancelled() || sec >= maxWait) break;
                    await m_wait(PollInterval);
                    if (cancelled()) break;
                    q = await m_server.Poll();
                    if (Matched(q)) return q.Match;
                    if (q?.Status == QueueStatus.Idle)
                    {
                        q = await m_server.Queue(); // 대기열 만료 → 다시 등록
                        if (Matched(q)) return q.Match;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 상대 찾기 실패 - 봇으로: " + ex.Message); }
            try { await m_server.Cancel(); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 대기열 취소 실패: " + ex.Message); }
            return null;
        }

        static bool Matched(QueueStatus q) => q != null && q.Status == QueueStatus.Matched && q.Match != null;

        public async Task<OnlineOpponent> Connect(MatchInfo m, Func<bool> cancelled)
        {
            double t0 = m_clock();
            IPvpLink link = null;
            try
            {
                string code = null;
                if (m.Host) (link, code) = await m_links.Host();
                MatchState st = await m_server.Ready(m.MatchId, code ?? "");
                while (true)
                {
                    if (st == null || st.Status == MatchState.Cancelled || st.Status == MatchState.Done) break;
                    if (link == null && !string.IsNullOrEmpty(st.RelayJoinCode)) link = await m_links.Join(st.RelayJoinCode);
                    if (st.Status == MatchState.Playing && link != null)
                        return new OnlineOpponent(m, Math.Max(0, (st.StartAt - st.Now) / 1000.0), m_server, link, m_clock);
                    if (cancelled() || m_clock() - t0 >= ReadyTimeout) break;
                    await m_wait(PollInterval);
                    if (cancelled()) break;
                    st = await m_server.Ping(m.MatchId);
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 상대와 연결 실패: " + ex.Message); }

            link?.Close();
            try { await m_server.Leave(m.MatchId); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 경기 나가기 실패: " + ex.Message); }
            return null;
        }
    }
}
