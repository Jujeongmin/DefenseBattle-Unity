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
        string m_ticket;
        bool m_done;

        public string Nick { get; }
        public int Trophies { get; }
        public event Action<AttackPayload> Attack;
        public event Action<BoardSnapshot> Snapshot;
        public event Action<PvpOutcome> Finished;

        public BotOpponent(BotMatch bot, string nick, int trophies)
        {
            m_bot = bot;
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
        public BotMatch Match => m_bot;

        public async Task Begin()
        {
            try { m_ticket = (await CloudClient.Call<RunTicket>(CloudApi.PvpBotStart))?.Id; }
            catch (Exception ex) { Debug.LogWarning("[Pvp] 봇전 시작 기록 실패 - 트로피 미반영: " + ex.Message); }
        }

        public void Tick(double realDt) => m_bot.Tick(realDt);
        public void SendAttack(AttackPayload p) => m_bot.ReceiveAttack(p);
        public void SendSnapshot(BoardSnapshot s) => m_bot.SetOpponent(s);
        public void ReportDefeat() => _ = Finish(false);

        public void Leave()
        {
            m_bot.Stop();
        }

        async Task Finish(bool playerWon)
        {
            if (m_done) return;
            m_done = true;
            m_bot.Stop();
            var o = new PvpOutcome { Won = playerWon, Trophies = EconCache.State.Pvp.Trophies };
            if (m_ticket != null)
            {
                try
                {
                    BotResultReply r = await CloudClient.Call<BotResultReply>(CloudApi.PvpBotResult, CloudClient.Args("id", m_ticket, "won", playerWon));
                    if (r != null)
                    {
                        o.Delta = r.Delta;
                        o.Trophies = r.Trophies;
                        o.Reward = r.Reward;
                        if (r.Econ != null) EconCache.Apply(r.Econ);
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[Pvp] 봇전 결과 저장 실패: " + ex.Message); }
            }
            Finished?.Invoke(o);
        }
    }
}
