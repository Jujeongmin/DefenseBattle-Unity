using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 덱 화면 규칙 (progress.ts toggleDeck, menu.ts unitStatsHtml · deck-warn · 카드 정렬 · 추천 덱).
    /// 덱 편집은 화면 쪽 사본(List)에 적용하고, 창을 닫을 때 서버 SetDecks 로 보낸다.
    /// </summary>
    public static class DeckRules
    {
        /// <summary>넣기/빼기: 덱에 있으면 빼고(최소 1종), 없으면 빈 칸에 넣는다 (가득 차면 실패)</summary>
        public static bool Toggle(EconState e, List<Job> deck, Job job)
        {
            if (deck.Contains(job)) return Progress.RemoveDeck(deck, job);
            if (!e.Owns(job) || deck.Count >= Units.DeckSize) return false;
            deck.Add(job);
            return true;
        }

        /// <summary>카드 공격력 (일반 등급 기준, engine unitDamage 와 같은 식): 판 안에서는 등급마다 더 오른다</summary>
        public static int UnitAttack(Job job, int level)
        {
            UnitDef u = Units.Get(job);
            Grade g = Grades.All[0];
            double baseAtk = (g.AtkMin + g.AtkMax) / 2;
            double v = baseAtk * u.AtkMult * Units.RarityAtk[(int)u.Rarity] * (1 + (Math.Max(1, level) - 1) * Units.LevelAtk);
            return (int)Math.Round(v, MidpointRounding.AwayFromZero); // Math.round (0.5 올림)
        }

        /// <summary>덱 빈 칸 수 (가진 유닛이 더 있는데 덱이 덜 찬 만큼 - 소환이 비싸진다)</summary>
        public static int Gap(EconState e, IList<Job> deck) => Math.Max(0, Progress.DeckMin(e) - deck.Count);

        /// <summary>빈 칸이 gap 개일 때 소환 비용 (config.ts summonCost × (1 + gap × deckGapPenalty))</summary>
        public static int GapSummonCost(int gap)
            => (int)Math.Round(Economy.SummonCost * (1 + gap * Economy.DeckGapPenalty), MidpointRounding.AwayFromZero);

        /// <summary>유닛 카드 순서: 가진 유닛을 위로, 그 안에서는 원래 순서</summary>
        public static List<UnitDef> SortedUnits(EconState e)
        {
            var list = new List<UnitDef>(Units.All.Length);
            foreach (UnitDef u in Units.All) if (e.Owns(u.Job)) list.Add(u);
            foreach (UnitDef u in Units.All) if (!e.Owns(u.Job)) list.Add(u);
            return list;
        }

        /// <summary>몹 카드 순서: 쓸 수 있는 카드를 위로</summary>
        public static List<AttackDef> SortedAttacks(EconState e)
        {
            var list = new List<AttackDef>(Attacks.All.Length);
            foreach (AttackDef a in Attacks.All) if (EconRules.AttackUnlocked(e, a.Key)) list.Add(a);
            foreach (AttackDef a in Attacks.All) if (!EconRules.AttackUnlocked(e, a.Key)) list.Add(a);
            return list;
        }

        /// <summary>추천 덱에서 아직 없는 카드 수</summary>
        public static int Missing(EconState e, DeckPreset<Job> p)
        {
            int n = 0;
            foreach (Job j in p.Cards) if (!e.Owns(j)) n++;
            return n;
        }

        public static int Missing(EconState e, DeckPreset<AttackKey> p)
        {
            int n = 0;
            foreach (AttackKey k in p.Cards) if (!EconRules.AttackUnlocked(e, k)) n++;
            return n;
        }
    }
}
