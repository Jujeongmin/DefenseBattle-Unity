using System.Threading.Tasks;
using DefenseBattle.Core;

namespace DefenseBattle.Server
{
    // 봇전 (원본 pvpBotStart / pvpBotResult) - 라이브 상대가 없을 때 기기가 봇과 붙는다. 트로피 규칙은 실제 대전과 같다.
    public sealed partial class GameServer
    {
        /// <summary>봇전 최소 시간 (사용자 결정 2026-11-16): 이보다 빨리 온 승리 보고는 무시 - 원본에는 없던 조작 방지</summary>
        public const long BotMinMs = 5_000;

        public async Task<RunTicket> PvpBotStart(string playerId)
        {
            string id = NewId();
            long now = Now;
            await m_store.MutatePlayer<BotRecord, bool>(playerId, Keys.Bot, () => new BotRecord(), r =>
            {
                r.Id = id;
                r.T = now;
                return true;
            });
            return new RunTicket { Id = id };
        }

        /// <summary>
        /// 봇전 결과 - 패배는 경기 시간과 상관없이 반영 (일찍 나가 패배를 피하는 일이 없게).
        /// 승리는 시작 뒤 BotMinMs 가 지나야 인정 (그 전이면 기록만 지우고 null). 시작 기록 1개당 결과 1번만. 잘못된 id 면 null
        /// </summary>
        public async Task<BotResultReply> PvpBotResult(string playerId, string id, bool won)
        {
            long now = Now;
            Change<BotRecord> taken = await m_store.MutatePlayer<BotRecord, Change<BotRecord>>(playerId, Keys.Bot, () => new BotRecord(), r =>
            {
                if (r.Id == null || r.Id != id) return new Change<BotRecord> { Changed = false };
                var copy = new BotRecord { Id = r.Id, T = r.T };
                r.Id = null;
                if (won && now - copy.T < BotMinMs) return new Change<BotRecord>(); // 너무 빠른 승리: 기록만 지운다
                return new Change<BotRecord> { Value = copy };
            });
            if (taken.Value == null) return null;
            int delta = 0;
            EconReply<PvpRewardResult> rep = await WithEcon(playerId, e =>
            {
                if (!e.MarkProcessed("bot:" + id)) return null;
                delta = EconRules.ApplyPvpResult(e, won);
                return EconRules.PvpReward(e, won, Ctx());
            });
            if (rep.Result == null) return null;
            await m_rank.Submit(playerId, rep.Econ.Pvp.Trophies, rep.Econ.Pvp.Wins, await NickOf(playerId));
            return new BotResultReply { Delta = delta, Trophies = rep.Econ.Pvp.Trophies, Reward = rep.Result, Econ = rep.Econ };
        }
    }
}
