using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 결제 팝업 조건 (offers.ts) - 중요한 순간에 한정 상품을 띄운다.
    /// · 연패(대전 2연패 / 스테이지 2연속 실패) → 다시 일어서기, 새 아레나 → 승급 축하, 새 챕터 → 새 챕터 패키지
    /// · 트로피 로드에서 유닛이 열리기 바로 앞 단계 → 그 유닛 패키지 등장
    /// · 보석 부족 → 그 유닛 패키지(등장했으면) 또는 모자란 만큼의 가장 작은 보석 상품
    /// 판이 끝날 때 최대 하나, 같은 한정 상품 팝업은 하루 한 번, 한정 상품은 24시간 동안 상점에도 보인다.
    /// 기록(offers: id → 끝나는 시각, shownAt: id → 마지막 표시 시각)은 기기 저장 (LocalMeta).
    /// </summary>
    public static class OfferRules
    {
        const long Hour = 3_600_000L;

        /// <summary>이 한정 상품이 지금 떠 있나 (offers.ts offerActive)</summary>
        public static bool Active(IDictionary<string, long> offers, string id, long now)
            => offers.TryGetValue(id, out long until) && until > now;

        /// <summary>지금 떠 있는 한정 상품 (상점 '한정 특가' 칸)</summary>
        public static void ActiveOffers(IDictionary<string, long> offers, long now, List<ProductDef> into)
        {
            into.Clear();
            foreach (ProductDef p in Products.All)
                if (p.Category == "offer" && Active(offers, p.Id, now)) into.Add(p);
        }

        /// <summary>남은 시간 (ms, 0 이상)</summary>
        public static long MsLeft(IDictionary<string, long> offers, string id, long now)
            => Math.Max(0, (offers.TryGetValue(id, out long until) ? until : 0) - now);

        /// <summary>이미 가진 상품이라 살 수 없나 (shop.ts productOwned)</summary>
        public static bool ProductOwned(EconState e, string id, string seasonId)
        {
            ProductDef d = Products.Get(id);
            if (d == null) return false;
            if (id == "ad_remove") return e.AdRemoved;
            if (id == "season_premium") return e.Season.Premium && e.Season.Id == seasonId;
            return d.OneTime && e.OneTime.Contains(id);
        }

        /// <summary>유닛 패키지가 등장하는 트로피 = 로드에서 그 유닛 바로 앞 단계 (첫 단계면 0, 로드에 없으면 null)</summary>
        public static int? UnitPackOpenAt(Job job)
        {
            RoadStep[] steps = TrophyRoad.Steps;
            for (int i = 0; i < steps.Length; i++)
                if (steps[i].Unit == job) return i > 0 ? steps[i - 1].At : 0;
            return null;
        }

        static string PackKey(string id) => "pack:" + id;

        /// <summary>유닛 패키지를 지금 살 수 있나 (등장했고, 안 샀고, 그 유닛이 패키지 레벨 미만) → 상품 id 또는 null</summary>
        public static string UnitPackAvailable(EconState e, IDictionary<string, long> shownAt, Job job, string seasonId)
        {
            if (!Products.UnitPacks.TryGetValue(job, out string id) || ProductOwned(e, id, seasonId) || !shownAt.ContainsKey(PackKey(id))) return null;
            int lv = Products.Get(id).Grant.Units.TryGetValue(job, out int g) ? g : 5;
            return e.LevelOf(job) < lv ? id : null;
        }

        /// <summary>
        /// 새로 닿은 유닛 패키지 하나를 등장시킨다 (로드 순서대로, 한 번에 하나) → 팝업할 상품 id 또는 null.
        /// 이미 샀거나 Lv5 이상이면 조용히 등장 처리만 하고 다음으로 (offers.ts revealNextUnitPack)
        /// </summary>
        public static string NextUnitPack(EconState e, IDictionary<string, long> shownAt, long now, string seasonId)
        {
            foreach (RoadStep s in TrophyRoad.Steps)
            {
                if (s.Unit == null || !Products.UnitPacks.TryGetValue(s.Unit.Value, out string id)) continue;
                if (shownAt.ContainsKey(PackKey(id))) continue;
                int? at = UnitPackOpenAt(s.Unit.Value);
                if (at == null || e.Pvp.Trophies < at.Value) return null; // 아직 다음 패키지 지점 전
                shownAt[PackKey(id)] = now;
                if (ProductOwned(e, id, seasonId) || e.LevelOf(s.Unit.Value) >= 5) continue;
                return id;
            }
            return null;
        }

        /// <summary>한정 상품 띄우기 (kind: comeback / arena / chapter, 같은 팝업은 하루 한 번) → 띄울 상품 id 또는 null</summary>
        public static string TryOffer(IDictionary<string, long> offers, IDictionary<string, long> shownAt, string kind, long now)
        {
            string id = Products.OfferProduct[kind];
            if (shownAt.TryGetValue(id, out long last) && now - last < 24 * Hour) return null;
            if (!Active(offers, id, now)) offers[id] = now + Products.OfferHours * Hour;
            shownAt[id] = now;
            return id;
        }

        /// <summary>
        /// 판이 끝난 뒤 (결과 화면 위) 띄울 상품. 판마다 최대 하나: 승급 > 유닛 패키지 > 새 챕터 > 연패.
        /// 반환: 무엇 때문인지 (arena / pack / chapter / comeback, 없으면 null), productId: 상품 id
        /// </summary>
        public static string AfterMatch(EconState e, IDictionary<string, long> offers, IDictionary<string, long> shownAt, long now, string seasonId,
            bool tierUp, bool chapterUp, bool losing, out string productId)
        {
            if (tierUp && (productId = TryOffer(offers, shownAt, "arena", now)) != null) return "arena";
            if ((productId = NextUnitPack(e, shownAt, now, seasonId)) != null) return "pack";
            if (chapterUp && (productId = TryOffer(offers, shownAt, "chapter", now)) != null) return "chapter";
            if (losing && (productId = TryOffer(offers, shownAt, "comeback", now)) != null) return "comeback";
            productId = null;
            return null;
        }

        /// <summary>보석 부족: 그 유닛 패키지(살 수 있으면) 또는 need 를 채우는 가장 작은 보석 상품 (없으면 가장 큰 것)</summary>
        public static string ShortageProduct(EconState e, IDictionary<string, long> shownAt, int need, Job? job, string seasonId)
        {
            string pack = job.HasValue ? UnitPackAvailable(e, shownAt, job.Value, seasonId) : null;
            if (pack != null) return pack;
            ProductDef fit = null, biggest = null;
            foreach (ProductDef p in Products.All)
            {
                if (p.Category != "crystal") continue;
                int n = p.Grant.Crystals;
                if (biggest == null || n > biggest.Grant.Crystals) biggest = p;
                if (n >= need && (fit == null || n < fit.Grant.Crystals)) fit = p;
            }
            return (fit ?? biggest)?.Id;
        }
    }
}
