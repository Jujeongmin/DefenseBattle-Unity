using System;
using System.Collections.Generic;
using System.Linq;
using DefenseBattle.Core;

// ============================================================
//  통합 진행 시뮬레이터 (원본 tools/progsim.ts 이식)
//  새 계정 한 명이 캠페인과 대전을 번갈아 하며 보석으로 연구·유닛 강화를 산다.
//  실행: dotnet run -c Release --project CoreTests/Sim -- prog [모드=both|pvp] [실력=0.35] [시간=8] [판수=1]
//    both: 캠페인과 대전을 번갈아 (캠페인이 2번 연속 막히면 대전 3판 뒤 다시 도전)   pvp: 대전만
//  원본과 다른 점: 대전은 실제 게임처럼 양쪽 다 연구를 적용하고 (원본 main.ts·pvp.ts), 상대는 이식판 봇(BotMatch)을 그대로 쓴다.
//  빠진 것 (원본과 같음): 일일 퀘스트·업적·시즌 패스·챕터 별 상자 보석 - 실제로는 조금 더 빠르다.
// ============================================================
static class ProgSim
{
    const double Overhead = 20;      // progsim.ts OVERHEAD - 판마다 로비·매칭·결과 화면 (실시간 초)
    const double HumanThink = 0.7;   // progsim.ts HUMAN_THINK - 사람 손: 0.7초마다 한 번 판단
    const int GachaReserve = 400;    // progsim.ts GACHA_RESERVE - 이만큼 넘는 보석은 뽑기에 쓴다
    static readonly int[] s_marks = { 300, 550, 850, 1200, 1500, 2000 };

    static int NextInt(IRandom r) => SystemRandom.Index(r, int.MaxValue - 1) + 1;

    sealed class Run
    {
        public EconState E;
        public double Clock; // 실시간(초)
        public long T0;
        public SystemRandom Rng;
        public readonly List<string> Log = new List<string>();
        public readonly Dictionary<string, double> Milestones = new Dictionary<string, double>();
        public readonly HashSet<Job> Seen = new HashSet<Job>();
        public double CampaignSec, PvpSec;
        public int Stages, StageFails;

        public EconContext Ctx() => new EconContext(T0 + (long)(Clock * 1000), Rng);
        public string At => $"{Clock / 3600:F1}h";

        public void Mark(string key, string text)
        {
            if (Milestones.ContainsKey(key)) return;
            Milestones[key] = Clock / 3600;
            Log.Add($"{At}  {text}");
        }
    }

    public static void RunAll(string mode, double skill, double hours, int runs)
    {
        var all = new List<Run>();
        for (int i = 0; i < runs; i++)
        {
            Run r = Play(mode, skill, hours, 20261116 + i * 7919);
            all.Add(r);
            if (i == 0) Print(r, mode, skill, hours);
        }
        if (runs <= 1) return;

        Console.WriteLine($"\n=== {runs}명 평균 (시간, 도달 못 한 사람 수) ===");
        IEnumerable<string> keys = all.SelectMany(r => r.Milestones.Keys).Distinct()
            .OrderBy(k => all.Where(r => r.Milestones.ContainsKey(k)).Average(r => r.Milestones[k]));
        foreach (string k in keys)
        {
            List<double> v = all.Where(r => r.Milestones.ContainsKey(k)).Select(r => r.Milestones[k]).OrderBy(x => x).ToList();
            Console.WriteLine($"{k,-24} 평균 {v.Average(),5:F1}h  중앙 {v[v.Count / 2],5:F1}h  범위 {v[0]:F1}~{v[v.Count - 1]:F1}h  미도달 {runs - v.Count}");
        }
        Console.WriteLine($"캠페인 실패율 평균 {all.Average(r => r.StageFails * 100.0 / Math.Max(1, r.Stages)):F0}%  · 끝 트로피 평균 {all.Average(r => r.E.Pvp.Trophies):F0}");
    }

    static Run Play(string mode, double skill, double hours, int seed)
    {
        var r = new Run { Rng = new SystemRandom(seed), T0 = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() };
        r.E = EconState.Fresh(r.Ctx().SeasonId);
        r.E.Stages["1-1"] = 3; // 튜토리얼은 끝낸 상태에서 시작 (progsim.ts 와 같음)
        foreach (Job j in r.E.UnitLevels.Keys) r.Seen.Add(j);

        int failStreak = 0, pvpDebt = 0, chapterDone = 0;
        bool both = mode == "both";
        var hourly = new Dictionary<int, (int w, int l)>();
        while (r.Clock < hours * 3600)
        {
            bool campaignLeft = both && Campaign.Stages.Any(s => r.E.StarsOf(s.Id) == 0);
            if (campaignLeft && pvpDebt == 0)
            {
                StageDef st = Campaign.Stages.First(s => r.E.StarsOf(s.Id) == 0);
                bool ok = PlayStage(r, st, skill);
                if (ok)
                {
                    failStreak = 0;
                    int ch = Campaign.Stages.Where(s => r.E.StarsOf(s.Id) > 0).Max(s => s.Chapter);
                    if (ch > chapterDone && Campaign.Stages.Where(s => s.Chapter == ch).All(s => r.E.StarsOf(s.Id) > 0))
                    {
                        chapterDone = ch;
                        r.Mark($"캠페인 챕터 {ch} 완료", $"캠페인 챕터 {ch} 완료");
                    }
                    if (!Campaign.Stages.Any(s => r.E.StarsOf(s.Id) == 0)) r.Mark("캠페인 전부 클리어", "캠페인 전부 클리어");
                }
                else if (++failStreak >= 2) { pvpDebt = 3; failStreak = 0; } // 막히면 대전으로 보석 모아 오기
                if (pvpDebt == 0) pvpDebt = 1; // 스테이지 뒤엔 대전 한 판
            }
            else
            {
                int before = r.E.Pvp.Trophies;
                bool won = PlayPvp(r, skill);
                int h = (int)(r.Clock / 3600);
                hourly.TryGetValue(h, out (int w, int l) x);
                hourly[h] = won ? (x.w + 1, x.l) : (x.w, x.l + 1);
                foreach (int m in s_marks)
                    if (r.E.Pvp.Trophies >= m && before < m) r.Mark($"트로피 {m}", $"트로피 {m} 도달");
                pvpDebt = Math.Max(0, pvpDebt - 1);
            }
            TendChests(r);
            Spend(r, both);
        }
        r.Log.Add("시간별 대전 승률: " + string.Join("  ", hourly.OrderBy(k => k.Key)
            .Select(k => $"{k.Key}h {(int)Math.Round(k.Value.w * 100.0 / (k.Value.w + k.Value.l))}%({k.Value.w + k.Value.l}판)")));
        return r;
    }

    static void NoteUnlocks(Run r, string src)
    {
        foreach (UnitDef u in Units.All)
        {
            if (!r.E.Owns(u.Job) || r.Seen.Contains(u.Job)) continue;
            r.Seen.Add(u.Job);
            r.Mark($"유닛 {u.Job} 해금", $"유닛 {u.Job} 해금 ({src})");
        }
        if (r.Seen.Count == Units.All.Count()) r.Mark("유닛 전부 해금", "유닛 전부 해금");
    }

    /// <summary>덱: 해금된 유닛 중 레벨 높은 순 4종 (같으면 유닛 표 순서)</summary>
    static void ChooseDeck(EconState e)
    {
        List<Job> owned = Units.All.Where(u => e.Owns(u.Job)).Select(u => u.Job).ToList();
        e.Deck = owned.OrderByDescending(j => e.LevelOf(j)).Take(Units.DeckSize).ToList();
    }

    static Dictionary<Job, int> Levels(EconState e) => Units.All.Where(u => e.Owns(u.Job)).ToDictionary(u => u.Job, u => e.LevelOf(u.Job));

    /// <summary>상자: 다 된 건 열고, 여는 중이 없으면 하나 열기 시작</summary>
    static void TendChests(Run r)
    {
        for (int i = 0; i < r.E.Chests.Length; i++)
        {
            ChestSlot c = r.E.Chests[i];
            if (c != null && c.OpenAt.HasValue && EconRules.ChestLeftMs(c, r.Ctx().Now) <= 0) EconRules.OpenSlot(r.E, i, false, r.Ctx());
        }
        if (!r.E.Chests.Any(c => c != null && c.OpenAt.HasValue))
        {
            int i = Array.FindIndex(r.E.Chests, c => c != null && !c.OpenAt.HasValue);
            if (i >= 0) EconRules.StartUnlock(r.E, i, r.Ctx());
        }
        NoteUnlocks(r, "상자");
    }

    /// <summary>보석 쓰기: 연구·덱 유닛 강화 중 가장 싼 것부터, 남는 보석은 뽑기</summary>
    static void Spend(Run r, bool research)
    {
        EconState e = r.E;
        for (;;)
        {
            int best = int.MaxValue;
            Action buy = null;
            if (research)
            {
                foreach (ResearchDef d in Research.All)
                {
                    int lv = e.ResearchLevel(d.Key);
                    int cost = Research.Cost(lv);
                    if (lv < d.Max && cost <= e.Crystals && cost < best)
                    {
                        best = cost;
                        ResearchKey k = d.Key;
                        buy = () => { e.Crystals -= cost; e.Research[k] = lv + 1; };
                    }
                }
            }
            foreach (Job j in e.Deck)
            {
                int lv = e.LevelOf(j);
                int cost = Units.UpgradeCost(lv);
                bool cards = lv > 0 && lv < Units.MaxLevel && e.CardCount(j) >= Units.CardsNeeded(Units.Get(j).Rarity, lv);
                if (cards && cost <= e.Crystals && cost < best)
                {
                    best = cost;
                    buy = () => EconRules.UpgradeUnit(e, j);
                }
            }
            if (buy == null) break;
            buy();
        }
        while (e.Crystals >= GachaReserve + Chests.GachaOne) EconRules.Gacha(e, 1, r.Ctx());
        NoteUnlocks(r, "뽑기");
        if (Research.All.All(d => e.ResearchLevel(d.Key) >= d.Max)) r.Mark("연구 MAX", "연구 전부 MAX");
    }

    /// <summary>캠페인 한 판 (사람 손 속도)</summary>
    static bool PlayStage(Run r, StageDef st, double skill)
    {
        EconState e = r.E;
        ChooseDeck(e);
        GameOptions o = GameOptions.ForStage(st);
        o.Track = false;
        o.Deck = new List<Job>(e.Deck);
        o.DeckMin = Math.Min(Units.DeckSize, e.UnitLevels.Count(kv => kv.Value > 0));
        o.UnitLevels = Levels(e);
        o.Research = new Dictionary<ResearchKey, int>(e.Research);
        o.RandomSeed = NextInt(r.Rng);
        o.Seed = (uint)NextInt(r.Rng);
        var g = new Game(o);
        var ai = new BotPlayer(g, skill, r.Rng);
        double t = 0, act = 0;
        while (!g.Over && t < 60 * 60)
        {
            g.Update(0.05);
            t += 0.05;
            act += 0.05;
            if (act >= HumanThink * Waves.PlaySpeed) { act = 0; ai.Step(); }
        }
        double sec = t / Waves.PlaySpeed + Overhead;
        r.Clock += sec;
        r.CampaignSec += sec;
        r.Stages++;
        if (!g.Cleared) r.StageFails++;
        EconRules.StageEnd(e, st.Id, g.Cleared, g.Cleared ? Campaign.StarsFor(g.PeakMobs) : 0, g.Wave, r.Ctx());
        NoteUnlocks(r, "스테이지 " + st.Id);
        return g.Cleared;
    }

    /// <summary>대전 한 판 - 상대는 이식판 실제 봇 (BotMatch)</summary>
    static bool PlayPvp(Run r, double skill)
    {
        EconState e = r.E;
        ChooseDeck(e);
        PvpStats p = e.Pvp;
        uint seed = (uint)NextInt(r.Rng);
        double sk = BotProfile.Skill(p.Trophies, p.Wins, p.Losses, p.Streak, p.LossStreak);
        int oppTrophies = BotMatch.OpponentTrophies(p.Trophies, r.Rng);
        Dictionary<Job, int> levels = Levels(e);
        var a = new Game(new GameOptions
        {
            // 원본 main.ts 실제 대전과 같이 내 연구 적용 (progsim.ts 는 양쪽 다 연구 없이 돌렸다)
            Mode = GameMode.Pvp, Seed = seed, UseResearch = true, Research = new Dictionary<ResearchKey, int>(e.Research), Track = false,
            Deck = new List<Job>(e.Deck), DeckMin = Math.Min(Units.DeckSize, e.UnitLevels.Count(kv => kv.Value > 0)),
            AttackDeck = new List<AttackKey>(e.AttackDeck), UnitLevels = levels, RandomSeed = NextInt(r.Rng),
        });
        var bot = new BotMatch(seed, sk, oppTrophies, BotProfile.Research(oppTrophies, p.Wins + p.Losses), BotProfile.Level(e.Deck, levels), r.Rng);
        var ai = new BotPlayer(a, skill, r.Rng);
        BoardSnapshot botSnap = null;
        a.AttackSent += x => bot.ReceiveAttack(x);
        bot.AttackSent += x => a.ReceiveAttack(x);
        bot.Snapshot += s => botSnap = s;

        const double realDt = 0.05 / Waves.PlaySpeed; // 게임 0.05초 = 실시간 0.025초
        double t = 0, act = 0, snap = 0;
        while (!a.Over && !bot.Game.Over && t < 60 * 15)
        {
            a.Update(0.05);
            bot.Tick(realDt);
            t += realDt;
            act += realDt;
            snap += realDt;
            if (snap >= PvpConfig.ReportInterval) { snap = 0; bot.SetOpponent(a.Snapshot()); }
            if (act >= HumanThink) { act = 0; ai.Step(botSnap); }
        }
        bool won = bot.Game.Over && !a.Over ? true : a.Over && !bot.Game.Over ? false : a.Mobs.Count < bot.Game.Mobs.Count;
        double sec = t + Overhead;
        r.Clock += sec;
        r.PvpSec += sec;
        EconRules.ApplyPvpResult(e, won);
        EconRules.PvpReward(e, won, r.Ctx()); // 보석·시즌 XP·승리 상자·트로피 로드 (서버와 같은 규칙)
        NoteUnlocks(r, "트로피 " + p.Trophies);
        return won;
    }

    static void Print(Run r, string mode, double skill, double hours)
    {
        EconState e = r.E;
        Console.WriteLine($"\n=== {(mode == "both" ? "캠페인 + 대전" : "대전만")} · 실력 {skill} · {hours}시간 ===");
        foreach (string l in r.Log) Console.WriteLine(l);
        int cleared = Campaign.Stages.Count(s => e.StarsOf(s.Id) > 0);
        Console.WriteLine($"끝: 트로피 {e.Pvp.Trophies} · {e.Pvp.Wins}승 {e.Pvp.Losses}패 · 캠페인 {cleared}/{Campaign.Stages.Length} (판 {r.Stages}, 실패 {r.StageFails}) · 보석 {e.Crystals}");
        Console.WriteLine($"시간 배분: 캠페인 {r.CampaignSec / 3600:F1}h · 대전 {r.PvpSec / 3600:F1}h");
        Console.WriteLine("유닛 레벨: " + string.Join(", ", Units.All.Where(u => e.Owns(u.Job)).Select(u => $"{u.Job} {e.LevelOf(u.Job)}")));
        Console.WriteLine("연구: " + string.Join(", ", Research.All.Select(d => $"{d.Key} {e.ResearchLevel(d.Key)}")));
        Console.WriteLine($"덱: {string.Join(", ", e.Deck)} · 해금 유닛 {r.Seen.Count}/{Units.All.Count()}");
    }
}
