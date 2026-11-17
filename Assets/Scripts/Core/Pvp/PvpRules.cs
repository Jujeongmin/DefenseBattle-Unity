using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>봇 대전 세팅 (menu.ts startMatchmaking, pvp.ts findMatch·createBotSession·BotSession.ready, main.ts launch pvp)</summary>
    public static class PvpRules
    {
        /// <summary>첫 대전(배우는 판)은 찾기 화면 없이 바로 봇과 시작 (menu.ts startMatchmaking)</summary>
        public static bool IsFirstMatch(EconState e) => e.Pvp.Wins + e.Pvp.Losses == 0;

        /// <summary>상대 찾는 연출 1.5~3.5초 (pvp.ts findMatch 오프라인 경로 - 실시간 매칭은 6단계)</summary>
        public static double SearchSeconds(IRandom r) => 1.5 + r.Next() * 2.0;

        /// <summary>내 전장 옵션: 경제 상태의 덱·몹 카드·레벨·연구 (main.ts launch pvp)</summary>
        public static GameOptions PlayerOptions(EconState e, uint seed) => new GameOptions
        {
            Mode = GameMode.Pvp,
            Seed = seed,
            Deck = new List<Job>(e.Deck),
            DeckMin = Progress.DeckMin(e),
            AttackDeck = new List<AttackKey>(e.AttackDeck),
            UnitLevels = new Dictionary<Job, int>(e.UnitLevels),
            Research = new Dictionary<ResearchKey, int>(e.Research),
            UseResearch = true,
        };

        /// <summary>봇 상대 (pvp.ts createBotSession + BotSession.ready): 실력·연구는 내 전적·트로피, 유닛 레벨은 내 덱 평균</summary>
        public static BotMatch MakeBot(EconState e, uint seed, IRandom r)
        {
            PvpStats p = e.Pvp;
            double skill = BotProfile.Skill(p.Trophies, p.Wins, p.Losses, p.Streak, p.LossStreak);
            int botTrophies = BotMatch.OpponentTrophies(p.Trophies, r);
            return new BotMatch(seed, skill, botTrophies, BotProfile.Research(botTrophies, p.Wins + p.Losses),
                BotProfile.Level(e.Deck, e.UnitLevels), r);
        }

        /// <summary>경과 시간 "m:ss"</summary>
        public static string Clock(double seconds)
        {
            int s = Math.Max(0, (int)seconds);
            return s / 60 + ":" + (s % 60).ToString("00");
        }
    }
}
