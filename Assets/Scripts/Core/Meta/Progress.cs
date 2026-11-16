using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 진행 규칙 (progress.ts): 캠페인 해금·다음 스테이지·챕터 별, 기능 해금, 덱 편성, 판 시작 옵션, 트로피 로드.
    /// 서버가 보낸 EconState 를 읽기만 한다 (덱 편성은 화면 쪽 사본에 적용하고 SetDecks 로 서버에 보낸다).
    /// </summary>
    public static class Progress
    {
        // ---------------- 스테이지 ----------------
        public static bool StageCleared(EconState e, string id) => e.StarsOf(id) > 0;

        /// <summary>첫 스테이지이거나 이전 스테이지를 클리어했으면 도전 가능</summary>
        public static bool StageUnlocked(EconState e, string id)
        {
            StageDef st = Campaign.Get(id);
            if (st == null) return false;
            return st.Index == 0 || StageCleared(e, Campaign.Stages[st.Index - 1].Id);
        }

        /// <summary>다음에 도전할 스테이지 (모두 클리어 시 마지막)</summary>
        public static string NextStageId(EconState e)
        {
            foreach (StageDef s in Campaign.Stages) if (!StageCleared(e, s.Id)) return s.Id;
            return Campaign.Stages[Campaign.Stages.Length - 1].Id;
        }

        public static int ChapterStars(EconState e, int chapter)
        {
            int n = 0;
            foreach (StageDef s in Campaign.Stages) if (s.Chapter == chapter) n += e.StarsOf(s.Id);
            return n;
        }

        public static bool ChapterUnlocked(EconState e, int chapter)
            => chapter == 1 || StageCleared(e, (chapter - 1) + "-" + Campaign.StagesPerChapter);

        /// <summary>챕터 별 보상 수령 여부 (EconRules.ClaimStarReward 의 1회 키)</summary>
        public static bool StarRewardClaimed(EconState e, int chapter, int goalIdx) => e.OneTime.Contains("stars:" + chapter + ":" + goalIdx);

        // ---------------- 기능 해금 ----------------
        /// <summary>Campaign.FeatureUnlock 의 키 (pvp, units, research, quests, endless …)</summary>
        public static bool FeatureUnlocked(EconState e, string feature)
            => Campaign.FeatureUnlock.TryGetValue(feature, out string stage) && StageCleared(e, stage);

        // ---------------- 유닛·덱 ----------------
        /// <summary>레벨업 카드가 다 모였나 (보석은 따로)</summary>
        public static bool CardsReady(EconState e, Job job)
        {
            int lv = e.LevelOf(job);
            return lv > 0 && lv < Units.MaxLevel && e.CardCount(job) >= Units.CardsNeeded(Units.Get(job).Rarity, lv);
        }

        public static bool CanUpgradeUnit(EconState e, Job job) => CardsReady(e, job) && e.Crystals >= Units.UpgradeCost(e.LevelOf(job));

        /// <summary>끌어다 놓기: 덱 slot 칸에 놓는다. 이미 덱에 있으면 두 칸을 바꾸고, 칸이 차 있으면 교체, 빈 칸이면 뒤에 붙인다</summary>
        public static bool PlaceDeck(EconState e, List<Job> deck, Job job, int slot)
        {
            if (!e.Owns(job) || slot < 0 || slot >= Units.DeckSize) return false;
            int from = deck.IndexOf(job);
            if (slot >= deck.Count)
            {
                if (from >= 0) deck.RemoveAt(from);
                deck.Add(job);
            }
            else if (from >= 0)
            {
                Job t = deck[from];
                deck[from] = deck[slot];
                deck[slot] = t;
            }
            else
            {
                deck[slot] = job;
            }
            return true;
        }

        /// <summary>덱에서 뺀다 (최소 1장)</summary>
        public static bool RemoveDeck(List<Job> deck, Job job)
        {
            int i = deck.IndexOf(job);
            if (i < 0 || deck.Count <= 1) return false;
            deck.RemoveAt(i);
            return true;
        }

        /// <summary>몹 카드를 slot 칸에 (이미 덱에 있으면 두 칸을 바꾼다, 늘 4장)</summary>
        public static bool PlaceAttack(EconState e, List<AttackKey> deck, AttackKey key, int slot)
        {
            if (!EconRules.AttackUnlocked(e, key) || slot < 0 || slot >= deck.Count) return false;
            int from = deck.IndexOf(key);
            if (from >= 0)
            {
                AttackKey t = deck[from];
                deck[from] = deck[slot];
                deck[slot] = t;
            }
            else
            {
                deck[slot] = key;
            }
            return true;
        }

        /// <summary>덱에 넣을 수 있는 최대 유닛 수 (보유 유닛 수와 덱 칸 중 작은 값) - 이보다 적게 넣으면 소환이 비싸진다</summary>
        public static int DeckMin(EconState e)
        {
            int owned = 0;
            foreach (KeyValuePair<Job, int> kv in e.UnitLevels) if (kv.Value > 0) owned++;
            return owned < Units.DeckSize ? owned : Units.DeckSize;
        }

        // ---------------- 판 시작 옵션 ----------------
        public static GameOptions StageOptions(EconState e, string id, bool goldBooster)
        {
            GameOptions o = GameOptions.ForStage(Campaign.Get(id));
            Fill(o, e, goldBooster);
            return o;
        }

        public static GameOptions EndlessOptions(EconState e, bool goldBooster)
        {
            var o = new GameOptions();
            Fill(o, e, goldBooster);
            return o;
        }

        static void Fill(GameOptions o, EconState e, bool goldBooster)
        {
            o.Deck = new List<Job>(e.Deck);
            o.DeckMin = DeckMin(e);
            o.UnitLevels = new Dictionary<Job, int>(e.UnitLevels);
            o.Research = new Dictionary<ResearchKey, int>(e.Research);
            o.UseResearch = true;
            o.StartGoldMult = goldBooster ? 2 : 1;
        }

        // ---------------- 트로피 로드 ----------------
        /// <summary>다음 로드 보상 (아직 안 받았고 트로피가 모자란 첫 단계, 없으면 null)</summary>
        public static RoadStep NextRoadStep(EconState e)
        {
            foreach (RoadStep s in TrophyRoad.Steps)
                if (!e.RoadClaimed.Contains(s.At) && s.At > e.Pvp.Trophies) return s;
            return null;
        }
    }
}
