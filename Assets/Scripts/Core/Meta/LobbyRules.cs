using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 로비 표시 규칙 (chests.ts chestState·chestLeftMs·clockText·leftText, menu.ts 로드 바·NEW·빨간 점).
    /// 시각은 ms (Date.now() 와 같은 기준) 를 받아 계산만 한다.
    /// </summary>
    public static class LobbyRules
    {
        const long Hour = 3600_000L;

        /// <summary>여는 중인 상자 칸 (없으면 -1)</summary>
        public static int OpeningSlot(EconState e)
        {
            for (int i = 0; i < e.Chests.Length; i++) if (e.Chests[i]?.OpenAt != null) return i;
            return -1;
        }

        public static ChestStatus Status(EconState e, int i, long nowMs)
        {
            ChestSlot c = e.Chests[i];
            if (c == null) return ChestStatus.Empty;
            if (c.OpenAt == null) return ChestStatus.Locked;
            return c.OpenAt.Value <= nowMs ? ChestStatus.Ready : ChestStatus.Opening;
        }

        /// <summary>남은 시간: 여는 중이면 남은 만큼, 잠겨 있으면 상자 전체 시간</summary>
        public static long LeftMs(EconState e, int i, long nowMs)
        {
            ChestSlot c = e.Chests[i];
            if (c == null) return 0;
            if (c.OpenAt != null) return Math.Max(0, c.OpenAt.Value - nowMs);
            return (long)(Chests.Get(c.Kind).Hours * Hour);
        }

        /// <summary>시:분:초 (1시간 미만이면 분:초)</summary>
        public static string ClockText(long ms)
        {
            long t = Math.Max(0, (long)Math.Ceiling(ms / 1000.0));
            long h = t / 3600, m = t % 3600 / 60, s = t % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m.ToString("00") + ":" + s.ToString("00");
        }

        /// <summary>"3시간 5분" / "30분"</summary>
        public static string LeftText(long ms)
        {
            long m = (long)Math.Ceiling(ms / 60000.0);
            if (m >= 60) return Loc.Tl("{0}시간{1}", m / 60, m % 60 != 0 ? Loc.Tl(" {0}분", m % 60) : "");
            return Loc.Tl("{0}분", m);
        }

        /// <summary>로드 바: 지난 단계 트로피 → 다음 보상 트로피 사이 진행 (0~100, 다음 보상이 없으면 100)</summary>
        public static double RoadPercent(EconState e)
        {
            RoadStep next = Progress.NextRoadStep(e);
            if (next == null) return 100;
            int trophies = e.Pvp.Trophies, prevAt = 0;
            foreach (RoadStep s in TrophyRoad.Steps) if (s.At <= trophies) prevAt = s.At;
            return Math.Max(0, Math.Min(100, (trophies - prevAt) * 100.0 / (next.At - prevAt)));
        }

        /// <summary>도달했지만 아직 안 받은 로드 보상이 있나 (있으면 ClaimRoad)</summary>
        public static bool RoadUnclaimed(EconState e)
        {
            foreach (RoadStep s in TrophyRoad.Steps)
                if (e.Pvp.Trophies >= s.At && !e.RoadClaimed.Contains(s.At)) return true;
            return false;
        }

        /// <summary>열렸지만 아직 안 본 기능 (NEW 표)</summary>
        public static bool FeatureNew(EconState e, ICollection<string> seen, string feature)
            => Progress.FeatureUnlocked(e, feature) && !seen.Contains(feature);

        /// <summary>덱 메뉴 빨간 점 키: 레벨업할 수 있는 유닛과 레벨 (없으면 null). 본 키와 다르면 점을 띄운다</summary>
        public static string UnitsDotKey(EconState e)
        {
            string key = null;
            foreach (UnitDef u in Units.All)
            {
                if (!Progress.CanUpgradeUnit(e, u.Job)) continue;
                key = (key == null ? "units:" : key + ",") + u.Job + e.LevelOf(u.Job);
            }
            return key;
        }
    }
}
