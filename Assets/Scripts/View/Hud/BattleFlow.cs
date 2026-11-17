using DefenseBattle.Core;
using UnityEngine;

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
            _ = m_boot.Run(() => { Profile.EnsureSynced(); ShowLobby(); });
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
            m_runner.StartGame(l);
            m_run.Begin(l, m_runner.Game);
        }

        void OnGameOver(GameOverData d)
        {
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

        void ToLobby()
        {
            CloseResult();
            m_pausedForConfirm = false;
            m_finishing = false;
            m_run.Abandon();
            m_runner.Stop();
            ShowLobby();
        }
    }
}
