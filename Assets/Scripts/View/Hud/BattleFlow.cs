using System;
using System.Collections;
using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 판 흐름 (main.ts launch / onGameOver / finishSolo / exitGame): 접속 → 로비 → 판 → 결과(부활) → 다시 하기 / 로비.
    /// 결과·확인 창이 열려 있으면 구역 드래그를 막는다.
    /// </summary>
    public sealed class BattleFlow : MonoBehaviour
    {
        [SerializeField] BattleRunner m_runner;
        [SerializeField] BattleHud m_hud;
        [SerializeField] ResultPanel m_result;
        [SerializeField] Lobby m_lobby;
        [SerializeField] ZoneDragInput m_drag;
        [SerializeField] Toast m_toast;
        [SerializeField] BattleAudio m_audio;
        [SerializeField] BootPanel m_boot;
        [SerializeField] HeroPanel m_heroes;

        GameOverData m_last;
        bool m_pausedForConfirm;
        bool m_tutorialRun;
        bool m_finishing;
        ResultView m_shown;
        readonly RunSession m_run = new RunSession();

        void Awake()
        {
            m_runner.GameOver += OnGameOver;
            m_hud.ExitRequested += OnExit;
            m_lobby.Play += OnChosen;
            m_runner.GameStarted += g => m_audio.Bind(g); // 전투 음악 + 엔진 소리 (다시 하기 포함)
            m_run.Notice += m => m_toast.Show(m);
        }

        /// <summary>접속(로그인·경제 동기화) 뒤 로비로 - 연결이 없으면 접속 화면이 다시 시도를 받는다</summary>
        void Start()
        {
            Localization.Init(m_runner.Catalog);
            Ui.Sounds = () => m_audio.Play(Sfx.Button);
            m_audio.SwitchBgm(Bgm.Menu);
            _ = m_boot.Run(() =>
            {
                Profile.EnsureSynced();
                // 처음 온 사람(1-1 을 아직 못 깸): 로비 없이 바로 1-1 튜토리얼 (main.ts boot)
                if (!Progress.FeatureUnlocked(EconCache.State, "pvp")) OnChosen(new BattleLaunch(BattleLaunch.Mode.Stage, "1-1"));
                else ShowLobby();
            });
        }

        // ---------------- 1-1 첫 판 튜토리얼 (main.ts startTutorial) ----------------
        // 게임을 멈춘 채 눌러야 할 곳만 밝혀 한 단계씩: 소환 3번(같은 유닛) → 영웅 → 합성 → 닫기 → 강화
        Spotlight m_spot;
        CanvasScaler m_scaler;
        readonly WaitForSecondsRealtime m_stepGap = new WaitForSecondsRealtime(0.12f); // 클릭 결과가 화면에 반영될 시간

        IEnumerator Tutorial(Game g)
        {
            Job job = g.Opts.Deck[0];
            var common = new KeyValuePair<Job, GradeKey>(job, GradeKey.Common);
            g.ScriptSummons(new[] { common, common, common });
            g.Paused = true;
            m_hud.TipsBlocked = true;
            if (m_scaler == null) m_scaler = m_toast.GetComponentInParent<CanvasScaler>();
            m_spot = Spotlight.Create(m_scaler);
            var steps = new[]
            {
                new KeyValuePair<Func<GameObject>, string>(() => m_hud.SummonTarget, Loc.Tl("<b>소환</b>을 눌러 영웅을 불러요 ({0}20)", Loc.Icon("coin"))),
                new KeyValuePair<Func<GameObject>, string>(() => m_hud.SummonTarget, Loc.Tl("한 번 더!")),
                new KeyValuePair<Func<GameObject>, string>(() => m_hud.SummonTarget, Loc.Tl("하나 더 부르면 <b>합성</b>할 수 있어요")),
                new KeyValuePair<Func<GameObject>, string>(() => m_hud.HeroTarget, Loc.Tl("같은 영웅 3명! <b>영웅</b>을 눌러요")),
                new KeyValuePair<Func<GameObject>, string>(m_heroes.ReadyCard, Loc.Tl("눌러서 <b>합성</b> - 한 등급 위로!")),
                new KeyValuePair<Func<GameObject>, string>(m_heroes.CloseTarget, Loc.Tl("창을 닫아요")),
                new KeyValuePair<Func<GameObject>, string>(m_hud.UpgradeTarget, Loc.Tl("<b>강화</b>하면 그 직업이 모두 세져요")),
            };
            foreach (KeyValuePair<Func<GameObject>, string> s in steps)
            {
                bool done = false;
                m_spot.Step(s.Key, s.Value, () => done = true);
                while (!done)
                {
                    if (!Alive(g)) { EndTutorial(); m_tutorialCo = null; yield break; }
                    yield return null;
                }
                yield return m_stepGap;
            }
            bool shown = false;
            m_spot.Message(Loc.Tl("준비 끝! 몹이 길 끝에 쌓이지 않게 막아요"), 1.8f, () => shown = true);
            while (!shown && Alive(g)) yield return null;
            EndTutorial();
            m_tutorialCo = null;
            if (!Alive(g)) yield break;
            g.Paused = false;
            LocalMeta.Data.Tutorial = 1;
            LocalMeta.Save();
        }

        bool Alive(Game g) => m_runner.Game == g && !g.Over;

        Coroutine m_tutorialCo;

        void StopTutorial()
        {
            if (m_tutorialCo != null) StopCoroutine(m_tutorialCo);
            m_tutorialCo = null;
            EndTutorial();
        }

        void EndTutorial()
        {
            if (m_spot != null) m_spot.Close();
            m_spot = null;
            m_hud.TipsBlocked = false;
        }

        void ShowLobby()
        {
            m_audio.Unbind();
            m_audio.SwitchBgm(Bgm.Menu);
            m_lobby.Show();
        }

        /// <summary>판 시작 (main.ts launch): 메타 옵션으로 열고, 서버에 시작 기록</summary>
        void OnChosen(BattleLaunch l)
        {
            CloseResult();
            m_lobby.Hide();
            // 첫 튜토리얼 판 (결과 반영 전에 판단)
            m_tutorialRun = l.Kind == BattleLaunch.Mode.Stage && l.StageId == "1-1" && !Progress.FeatureUnlocked(EconCache.State, "pvp");
            EndDuel(false);
            m_runner.StartGame(l);
            m_run.Begin(l, m_runner.Game);
            m_hud.SetExitHidden(m_tutorialRun);
            if (l.Kind == BattleLaunch.Mode.Pvp)
            {
                m_duel = new PvpDuel(m_runner.Game, l.Opponent);
                m_duel.Result += OnPvpResult;
                m_hud.ShowPvp(m_duel);
                DuelStarted?.Invoke(m_duel);
            }
            StopTutorial(); // 지난 판의 안내가 남아 있으면
            if (l.Kind == BattleLaunch.Mode.Stage && l.StageId == "1-1" && LocalMeta.Data.Tutorial == 0) m_tutorialCo = StartCoroutine(Tutorial(m_runner.Game));
        }

        void OnGameOver(GameOverData d)
        {
            m_hud.HideCoach();
            if (d.IsPvp) return; // 대전 결과는 PvpDuel 이 서버 판정을 받아 알린다
            m_last = d;
            ResultView revive = ResultFlow.ForGameOver(d, m_runner.Game, RewardAds.Service.IsReady);
            if (revive != null) ShowResult(revive);
            else Finish();
        }

        /// <summary>최종 결과: 서버 보상을 받은 뒤 보여 준다 (main.ts finishSolo)</summary>
        async void Finish()
        {
            if (m_finishing) return;
            m_finishing = true;
            m_drag.Blocked = true;
            m_result.Hide();
            BattleLaunch l = m_runner.Launch;
            GameOverData d = m_last;
            RunOutcome o = await m_run.End(l, d, m_tutorialRun);
            m_finishing = false;
            if (m_runner.Launch != l || m_runner.Game == null) return; // 기다리는 사이 로비로 나갔음
            ShowResult(ResultFlow.Final(d, l, o, EconCache.State));
            // 결제 팝업 조건 (main.ts): 새 챕터 도달 / 2연속 실패 - 튜토리얼 1-1 판은 제외, 무한 모드는 없음
            if (l.Kind == BattleLaunch.Mode.Stage && l.StageId != "1-1")
            {
                StageDef st = Campaign.Get(l.StageId);
                StageDef next = st.Index + 1 < Campaign.Stages.Length ? Campaign.Stages[st.Index + 1] : null;
                bool chapterUp = o.Stage != null && o.Stage.FirstClear && next != null && next.Chapter > st.Chapter;
                m_lobby.Offers.AfterMatch(false, chapterUp, LocalMeta.Data.StageFailStreak >= 2);
            }
        }

        void OnExit()
        {
            Game g = m_runner.Game;
            if (g == null || g.Over) { ToLobby(); return; }
            // 대전은 멈추지 않는다 (상대는 계속 진행) - 나가면 패배
            if (g.IsPvp) { m_pausedForConfirm = false; ShowResult(ResultFlow.PvpExitConfirm()); return; }
            // 확인 창이 떠 있는 동안 판을 멈춘다
            m_pausedForConfirm = !g.Paused;
            g.Paused = true;
            ShowResult(ResultFlow.ExitConfirm());
        }

        void ShowResult(ResultView v)
        {
            m_shown = v;
            m_drag.Blocked = true;
            m_result.Show(v, OnAction);
        }

        void CloseResult()
        {
            m_result.Hide();
            m_drag.Blocked = false;
        }

        void OnAction(ResultAction a)
        {
            Game g = m_runner.Game;
            m_audio.Play(Sfx.Button);
            switch (a)
            {
                case ResultAction.Revive:
                    RewardAds.Service.Show(ok =>
                    {
                        if (ok && g == m_runner.Game && g.Revive())
                        {
                            CloseResult();
                            m_audio.Play(Sfx.Merge); // main.ts: 부활하면 audio.merge()
                            m_audio.SwitchBgm(g.IsBossWave ? Bgm.Boss : Bgm.Battle); // 판 끝에서 멈춘 음악을 다시
                        }
                        else m_toast.Show(Loc.Tl("광고를 끝까지 보면 부활할 수 있어요"));
                    });
                    break;
                case ResultAction.GiveUp:
                    Finish();
                    break;
                case ResultAction.Retry:
                    OnChosen(m_runner.Launch.Again());
                    break;
                case ResultAction.Next:
                case ResultAction.StageInfo:
                    string id = FindStage(a);
                    ToLobby();
                    m_lobby.OpenStageInfo(id);
                    break;
                case ResultAction.Double:
                    DoubleReward();
                    break;
                case ResultAction.Lobby:
                    ToLobby();
                    break;
                case ResultAction.Rematch:
                    ToLobby();
                    m_lobby.QuickMatch();
                    break;
                case ResultAction.Close:
                    CloseResult();
                    if (g != null && m_pausedForConfirm) g.Paused = false;
                    m_pausedForConfirm = false;
                    break;
            }
        }

        string FindStage(ResultAction a)
        {
            foreach (ResultButton b in m_shown.Buttons) if (b.Action == a) return b.StageId;
            return m_runner.Launch.StageId;
        }

        /// <summary>광고 보고 직전 판 보상 한 번 더 (main.ts doubleReward - 금액은 서버가 정한다)</summary>
        void DoubleReward()
        {
            ResultView v = m_shown;
            RewardAds.Service.Show(async ok =>
            {
                if (!ok) return;
                int amount = 0;
                try { amount = await CloudClient.Econ<int>(CloudApi.AdReward, CloudClient.Args("doubleReward", true)); }
                catch (System.Exception ex) { Debug.LogWarning("[Flow] AdReward 실패: " + ex.Message); }
                if (amount <= 0) { m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요.")); return; }
                m_audio.Play(Sfx.Coin);
                m_toast.Show(Loc.Tl("보상 2배! {0} +{1}", Loc.Icon("gem"), amount * 2)); // 더 받은 양이 아니라 2배가 된 합계
                if (v != m_shown) return;
                v.Buttons.RemoveAll(b => b.Action == ResultAction.Double);
                for (int i = 0; i < v.Rewards.Count; i++)
                    if (v.Rewards[i].StartsWith(Loc.Icon("gem"))) { v.Rewards[i] += " <color=#7CFC7C>+" + amount + "</color>"; break; }
                ShowResult(v);
            });
        }

        // ---------------- 대전 (main.ts startPvp · showPvpResult) ----------------
        PvpDuel m_duel;
        readonly List<Reveal> m_pendingReveals = new List<Reveal>();

        /// <summary>대전 판이 열림 (PvP HUD·상대 미니맵이 붙는다)</summary>
        public event Action<PvpDuel> DuelStarted;
        /// <summary>대전 판이 내려감</summary>
        public event Action DuelEnded;

        void Update()
        {
            if (m_duel != null) m_duel.Tick(Time.deltaTime);
        }

        void OnPvpResult(PvpOutcome o)
        {
            Game g = m_runner.Game;
            m_hud.HideCoach();
            if (o.Reward != null) m_pendingReveals.AddRange(o.Reward.Reveals); // 로드 상자 등은 로비에서 공개
            ShowResult(ResultFlow.ForPvp(o, m_duel != null ? m_duel.Opponent.Nick : "", g != null ? g.Wave : 0, RewardAds.Service.IsReady));
            // 결제 팝업 조건 (main.ts): 새 아레나 승급 / 2연패
            LocalMetaData m = LocalMeta.Data;
            int arena = Arenas.IndexOf(EconCache.State.Pvp.Trophies);
            bool tierUp = m.ArenaMax >= 0 && arena > m.ArenaMax;
            if (arena > m.ArenaMax) { m.ArenaMax = arena; LocalMeta.Save(); }
            m_lobby.Offers.AfterMatch(tierUp, false, !o.Won && EconCache.State.Pvp.LossStreak >= 2);
        }

        /// <summary>대전을 내린다. reportDefeat = 판 도중 나가 패배로 기록</summary>
        void EndDuel(bool reportDefeat)
        {
            if (m_duel == null) return;
            m_duel.Result -= OnPvpResult;
            m_duel.Leave(reportDefeat);
            m_duel = null;
            m_hud.HidePvp();
            DuelEnded?.Invoke();
        }

        void ToLobby()
        {
            Game pg = m_runner.Game;
            EndDuel(pg != null && !pg.Over);
            StopTutorial();
            CloseResult();
            m_pausedForConfirm = false;
            m_finishing = false;
            m_run.Abandon();
            m_runner.Stop();
            ShowLobby();
            foreach (Reveal r in m_pendingReveals) m_lobby.ShowReveal(r);
            m_pendingReveals.Clear();
        }
    }
}
