using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 안내 순서 (menu.ts maybeGuide · ui.ts maybeTip): 로비에서 한 번씩 밝혀 주는 기능 버튼, 판 안에서 한 번씩 나오는 팁.
    /// 본 것은 기기 기록(seenFeatures)에 남는다 - 로비 안내는 기능 이름 그대로, 팁은 "tip:이름".
    /// </summary>
    public static class GuideRules
    {
        /// <summary>로비 안내 순서: 대전 → 덱 → 캠페인 → 연구 → 퀘스트 → 무한</summary>
        public static readonly string[] LobbyOrder = { "pvp", "units", "campaign", "research", "quests", "endless" };

        /// <summary>
        /// 다음에 밝힐 로비 버튼 (없으면 null). 1-1 튜토리얼을 끝낸 뒤(tutorial ≥ 2)부터.
        /// present(key) = 지금 화면에 그 버튼이 있나
        /// </summary>
        public static string NextLobbyGuide(EconState e, int tutorial, IList<string> seen, Func<string, bool> present)
        {
            if (tutorial < 2 || !Progress.FeatureUnlocked(e, "pvp")) return null;
            foreach (string key in LobbyOrder)
            {
                bool on;
                switch (key)
                {
                    case "pvp": on = true; break;
                    case "campaign": on = e.StarsOf("1-2") == 0; break; // 아직 1-2 를 안 깼을 때만
                    default: on = Progress.FeatureUnlocked(e, key); break;
                }
                if (on && !seen.Contains(key) && present(key)) return key;
            }
            return null;
        }

        public const string TipGap = "gap", TipPeek = "peek", TipLate = "late";

        /// <summary>
        /// 판 안 팁 하나 (없으면 null): 빈틈(유닛 3명 이상, 6초 뒤) → 대전 상대 전장 크게 보기(25초 뒤) → 대전 후반 몹 카드(8웨이브).
        /// 매 프레임 불러도 할당 없음
        /// </summary>
        public static string PickTip(int gapCount, int units, double time, bool pvp, int wave, IList<string> seen)
        {
            if (gapCount > 0 && units >= 3 && time > 6 && !seen.Contains("tip:" + TipGap)) return TipGap;
            if (pvp && time > 25 && !seen.Contains("tip:" + TipPeek)) return TipPeek;
            if (pvp && wave >= 8 && !seen.Contains("tip:" + TipLate)) return TipLate;
            return null;
        }
    }
}
