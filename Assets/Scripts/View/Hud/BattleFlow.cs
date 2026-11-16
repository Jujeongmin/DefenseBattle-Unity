using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 판 흐름 (main.ts launch / onGameOver / finishSolo / exitGame): 시작 화면 → 판 → 결과(부활) → 다시 하기 / 로비.
    /// 결과·확인 창이 열려 있으면 구역 드래그를 막는다.
    /// </summary>
    public sealed class BattleFlow : MonoBehaviour
    {
        [SerializeField] BattleRunner m_runner;
        [SerializeField] BattleHud m_hud;
        [SerializeField] ResultPanel m_result;
        [SerializeField] LaunchPanel m_launch;
        [SerializeField] ZoneDragInput m_drag;
        [SerializeField] Toast m_toast;
        [SerializeField] BattleAudio m_audio;
        [SerializeField] BootPanel m_boot;

        GameOverData m_last;
        bool m_pausedForConfirm;

        void Awake()
        {
            m_runner.GameOver += OnGameOver;
            m_hud.ExitRequested += OnExit;
            m_launch.Chosen += OnChosen;
            m_runner.GameStarted += g => m_audio.Bind(g); // 전투 음악 + 엔진 소리 (다시 하기 포함)
        }

        /// <summary>접속(로그인·경제 동기화) 뒤 로비로 - 연결이 없으면 접속 화면이 다시 시도를 받는다</summary>
        void Start()
        {
            m_audio.SwitchBgm(Bgm.Menu);
            _ = m_boot.Run(ShowLaunch);
        }

        void ShowLaunch()
        {
            m_audio.Unbind();
            m_audio.SwitchBgm(Bgm.Menu); // 시작 화면 = 로비 자리
            m_launch.Show();
        }

        void OnChosen(BattleLaunch l)
        {
            m_audio.Play(Sfx.Button);
            BattleLaunch.Current = l;
            CloseResult();
            m_runner.StartGame(l);
        }

        void OnGameOver(GameOverData d)
        {
            m_last = d;
            Game g = m_runner.Game;
            ShowResult(ResultFlow.ForGameOver(d, g, m_runner.Launch, RewardAds.Service.IsReady));
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
                    ShowResult(ResultFlow.Final(m_last, m_runner.Launch));
                    break;
                case ResultAction.Retry:
                    CloseResult();
                    m_runner.Restart();
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

        void ToLobby()
        {
            CloseResult();
            m_pausedForConfirm = false;
            m_runner.Stop();
            ShowLaunch();
        }
    }
}
