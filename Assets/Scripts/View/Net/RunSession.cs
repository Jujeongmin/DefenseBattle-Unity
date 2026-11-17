using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 판 하나의 서버 기록 (econ.ts runStart / stageResult / endlessResult, meta.ts addDailyProgress·unlockAchievement):
    /// 시작할 때 기록 id 를 받고, 끝나면 그 id 로 보상을 한 번 받는다. 기록이 없거나 거부되면 보상 없이 결과만.
    /// 일일 퀘스트 진행·최고 웨이브는 기기(LocalMeta), 업적 보상은 서버(ClaimAchievement).
    /// </summary>
    public sealed class RunSession
    {
        Task<string> m_runId;
        Game m_game;
        readonly HashSet<string> m_achievePending = new HashSet<string>();

        /// <summary>업적 달성 알림 (토스트)</summary>
        public event Action<string> Notice;

        /// <summary>판 시작: 골드 부스터 차감·시작 기록 요청 (기다리지 않는다 - 판은 바로 시작)</summary>
        public void Begin(BattleLaunch l, Game g)
        {
            Unhook();
            m_game = g;
            g.Progress += OnProgress;
            g.AchievementReached += OnAchievement;
            if (l.GoldBooster) _ = Safe(CloudClient.Econ<bool>(CloudApi.UseGoldBooster), false, "UseGoldBooster");
            // 대전은 서버가 PvpBotStart / PvpBotResult 로 따로 기록한다 (일일 퀘스트·업적 진행만 여기서)
            m_runId = l.Kind == BattleLaunch.Mode.Pvp ? null : Safe(StartRun(l), null, "RunStart");
        }

        /// <summary>판을 내림 (로비로) - 기기 진행 저장</summary>
        public void Abandon()
        {
            Unhook();
            m_runId = null;
            LocalMeta.Save();
        }

        static async Task<string> StartRun(BattleLaunch l)
        {
            RunTicket t = await CloudClient.Call<RunTicket>(CloudApi.RunStart,
                CloudClient.Args("endless", l.Kind == BattleLaunch.Mode.Endless, "stageId", l.StageId));
            return t?.Id;
        }

        /// <summary>판 끝 → 서버 보상 + 기기 기록 (main.ts finishSolo 앞부분)</summary>
        public async Task<RunOutcome> End(BattleLaunch l, GameOverData d, bool tutorialRun)
        {
            Unhook();
            int stars = d.Cleared ? Campaign.StarsFor(d.PeakMobs) : 0;
            RunEndResult r = null;
            Task<string> pending = m_runId;
            m_runId = null;
            string id = pending != null ? await pending : null;
            if (id != null)
            {
                var args = new Dictionary<string, object> { { "id", id }, { "cleared", d.Cleared }, { "stars", stars }, { "wave", d.Wave } };
                r = await Safe(CloudClient.Econ<RunEndResult>(CloudApi.RunEnd, args), null, "RunEnd");
            }

            LocalMetaData m = LocalMeta.Data;
            if (l.Kind == BattleLaunch.Mode.Stage)
            {
                if (d.Cleared && l.StageId == "1-1" && m.Tutorial < 2) m.Tutorial = 2;
                m.StageFailStreak = d.Cleared ? 0 : m.StageFailStreak + 1;
            }
            LocalMeta.Save();
            return new RunOutcome
            {
                Stage = r?.Stage,
                Crystals = r?.Crystals ?? 0,
                BestWave = m.BestWave,
                TutorialRun = tutorialRun,
                AdReady = RewardAds.Service.IsReady,
            };
        }

        void OnProgress(QuestStat stat, int amount)
        {
            LocalMeta.AddDaily(stat, amount);
            if (stat == QuestStat.BestWave && amount > LocalMeta.Data.BestWave) LocalMeta.Data.BestWave = amount;
        }

        async void OnAchievement(string key)
        {
            if (EconCache.State.Achievements.Contains(key) || !m_achievePending.Add(key)) return;
            AchievementDef a = Quests.GetAchievement(key);
            try
            {
                int n = await CloudClient.Econ<int>(CloudApi.ClaimAchievement, CloudClient.Args("key", key));
                if (n > 0 && a != null) Notice?.Invoke(Loc.Tl("{0} 업적 달성: {1} (+{2}{3})", Loc.Icon("medal"), Loc.Tl(a.Kr), a.Reward, Loc.Icon("gem")));
            }
            catch (Exception ex) { Debug.LogWarning("[Run] ClaimAchievement 실패: " + ex.Message); }
            finally { m_achievePending.Remove(key); }
        }

        void Unhook()
        {
            if (m_game == null) return;
            m_game.Progress -= OnProgress;
            m_game.AchievementReached -= OnAchievement;
            m_game = null;
        }

        static async Task<T> Safe<T>(Task<T> t, T fallback, string what)
        {
            try { return await t; }
            catch (Exception ex)
            {
                Debug.LogWarning("[Run] " + what + " 실패 - 이번 판 보상 없음: " + ex.Message);
                return fallback;
            }
        }
    }
}
