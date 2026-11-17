using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>받을 것 판정 (menu.ts questClaimable·seasonClaimable, shop.ts canClaimMonthly·monthlyDaysLeft)</summary>
    public static class MetaRules
    {
        const long Day = 86_400_000L;

        /// <summary>오늘 이 퀘스트 보상을 받았나 (서버 EconRules.ClaimQuest 의 1회 키)</summary>
        public static bool QuestClaimed(EconState e, string key, string today) => e.OneTime.Contains("quest:" + today + ":" + key);

        /// <summary>받을 수 있는 일일 퀘스트가 있나 (진행은 기기 기록)</summary>
        public static bool QuestClaimable(EconState e, IDictionary<string, int> progress, string today)
        {
            foreach (DailyQuestDef q in Quests.Daily)
            {
                progress.TryGetValue(q.Key, out int v);
                if (v >= q.Goal && !QuestClaimed(e, q.Key, today)) return true;
            }
            return false;
        }

        /// <summary>시즌 패스에 받을 칸이 있나 (도달한 단계의 무료 + 프리미엄이면 프리미엄)</summary>
        public static bool SeasonClaimable(EconState e)
        {
            int t = EconRules.SeasonTierOf(e);
            for (int i = 1; i <= t; i++)
            {
                if (!e.Season.ClaimedFree.Contains(i)) return true;
                if (e.Season.Premium && !e.Season.ClaimedPremium.Contains(i)) return true;
            }
            return false;
        }

        public static bool MonthlyActive(EconState e, long now) => e.Monthly.Expires > now;

        public static bool MonthlyClaimable(EconState e, long now, string today) => MonthlyActive(e, now) && e.Monthly.LastClaim != today;

        public static int MonthlyDaysLeft(EconState e, long now) => MonthlyActive(e, now) ? (int)Math.Ceiling((e.Monthly.Expires - now) / (double)Day) : 0;
    }
}
