using System;
using System.Threading.Tasks;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 봇 상대 (pvp.ts BotSession): Core BotMatch 가 봇 전장을 돌리고, 트로피·보상은 서버(PvpBotStart / PvpBotResult)가 정한다.
    /// 서버 시작 기록이 없으면(연결 실패) 대전은 하되 트로피는 바뀌지 않는다 (원본과 같음).
    /// </summary>
    public sealed class BotOpponent : IPvpOpponent
    {
        readonly BotMatch m_bot;
        readonly Func<Task<string>> m_start;
        readonly Func<string, bool, Task<BotResultReply>> m_report;
        Task<string> m_begin;
        string m_ticket;
        bool m_done;

        /// <summary>서버 응답을 기다리는 최대 시간 (초) - 넘으면 트로피 변화 없이 결과를 낸다 (PvpDuel 의 6초 안전장치보다 짧게)</summary>
        public float ReportTimeout = 5f;

        public string Nick { get; }
        public int Trophies { get; }
        public event Action<AttackPayload> Attack;
        public event Action<BoardSnapshot> Snapshot;
        public event Action<PvpOutcome> Finished;

        public BotOpponent(BotMatch bot, string nick, int trophies)
            : this(bot, nick, trophies, StartOnServer, ReportOnServer) { }

        /// <param name="start">봇전 시작 기록 → 기록 id (원본 pvpBotStart)</param>
        /// <param name="report">(id, 내가 이겼나) → 서버 결과 (원본 pvpBotResult)</param>
        public BotOpponent(BotMatch bot, string nick, int trophies, Func<Task<string>> start, Func<string, bool, Task<BotResultReply>> report)
        {
            m_bot = bot;
            m_start = start;
            m_report = report;
            Nick = nick;
            Trophies = trophies;
            bot.AttackSent += p => Attack?.Invoke(p);
            bot.Snapshot += s => Snapshot?.Invoke(s);
            bot.BotDefeated += () => _ = Finish(true);
        }

        /// <summary>내 경제 상태로 봇을 만든다 (닉·트로피는 실제 유저처럼)</summary>
        public static BotOpponent Create(EconState e)
        {
            var r = new SystemRandom();
            uint seed = (uint)(r.Next() * int.MaxValue);
            BotMatch bot = PvpRules.MakeBot(e, seed, r);
            return new BotOpponent(bot, BotMatch.Nick(r), bot.Trophies);
        }

        public uint Seed => m_bot.Game.Opts.Seed;
        public double StartDelay => PvpDuel.CountdownSec;
        public BotMatch Match => m_bot;

        static async Task<string> StartOnServer() => (await CloudClient.Call<RunTicket>(CloudApi.PvpBotStart))?.Id;

        static Task<BotResultReply> ReportOnServer(string id, bool won)
            => CloudClient.Call<BotResultReply>(CloudApi.PvpBotResult, CloudClient.Args("id", id, "won", won));

        public Task Begin()
        {
            if (m_begin == null) m_begin = SafeStart();
            return m_begin;
        }

        async Task<string> SafeStart()
        {
            try { m_ticket = await m_start(); }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 봇전 시작 기록 실패 - 트로피 미반영: " + ex.Message); }
            return m_ticket;
        }

        public void Tick(double realDt) => m_bot.Tick(realDt);
        public void SendAttack(AttackPayload p) => m_bot.ReceiveAttack(p);
        public void SendSnapshot(BoardSnapshot s) => m_bot.SetOpponent(s);
        public void ReportDefeat() => _ = Finish(false);

        public void Leave()
        {
            m_bot.Stop();
        }

        static async Task ApplyLate(Task<BotResultReply> call)
        {
            try
            {
                BotResultReply r = await call;
                if (r?.Econ != null) EconCache.Apply(r.Econ);
            }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 늦은 봇전 결과 실패: " + ex.Message); }
        }

        async Task Finish(bool playerWon)
        {
            if (m_done) return;
            m_done = true;
            m_bot.Stop();
            var o = new PvpOutcome { Won = playerWon, Trophies = EconCache.State.Pvp.Trophies };
            // 시작 기록이 아직 오는 중이면 기다렸다가 보고한다 (느린 연결에서 일찍 나가도 패배가 기록되게 - 원본은 시작 기록 뒤에 판을 열었다)
            if (m_begin != null) await Task.WhenAny(m_begin, Task.Delay(TimeSpan.FromSeconds(ReportTimeout)));
            if (m_ticket != null)
            {
                try
                {
                    Task<BotResultReply> call = m_report(m_ticket, playerWon);
                    if (await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(ReportTimeout))) == call)
                    {
                        BotResultReply r = await call;
                        if (r != null)
                        {
                            o.Delta = r.Delta;
                            o.Trophies = r.Trophies;
                            o.Reward = r.Reward;
                            if (r.Econ != null) EconCache.Apply(r.Econ);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Pvp] 봇전 결과 응답이 늦음 - 결과를 먼저 보여 준다");
                        _ = ApplyLate(call); // 늦게라도 오면 경제 상태는 맞춘다
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[Pvp] 봇전 결과 저장 실패: " + ex.Message); }
            }
            Finished?.Invoke(o);
        }
    }
}
