using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전투 진행 루프 (main.ts step + loop). 이 MonoBehaviour 하나만 엔진을 진행시키고,
    /// 그 뒤 View 들이 같은 프레임의 엔진 상태를 차례로 그린다 (각 View 가 자기 Update 로 엔진을 건드리지 않게).
    /// 도형 층: Under(1) 바닥·오라 / MobOver(11) 몹 상태·체력바 / Over(30) 이펙트 / Top(40) 보스바.
    /// </summary>
    public sealed class BattleRunner : MonoBehaviour
    {
        [SerializeField] SpriteCatalog m_catalog;
        [SerializeField] Camera m_camera;
        /// <summary>전장이 들어갈 화면 칸 (위·아래 UI 사이)</summary>
        [SerializeField] RectTransform m_boardArea;
        [SerializeField] BoardView m_board;
        [SerializeField] ShapeBatch m_under;
        [SerializeField] ShapeBatch m_mobOver;
        [SerializeField] ShapeBatch m_over;
        [SerializeField] ShapeBatch m_top;
        /// <summary>스프라이트용 Unlit 머티리얼 (2D 조명 없이 그대로 보이게)</summary>
        [SerializeField] Material m_spriteMaterial;
        /// <summary>피격 흰 번쩍임 (가산 합성)</summary>
        [SerializeField] Material m_flashMaterial;
        [SerializeField] ActorViews m_actors;

        readonly List<double> m_steps = new List<double>(8);

        public Game Game { get; private set; }
        public BattleLaunch Launch { get; private set; }
        public SpriteCatalog Catalog => m_catalog;
        public Camera Camera => m_camera;
        public Material SpriteMaterial => m_spriteMaterial;
        /// <summary>애니메이션 시계 (render.ts this.t: 실제 초 × PlaySpeed)</summary>
        public double AnimTime { get; private set; }

        /// <summary>새 판이 시작됨 (HUD 등이 새 Game 에 붙는다)</summary>
        public event Action<Game> GameStarted;
        /// <summary>판 끝 (결과·부활 흐름)</summary>
        public event Action<GameOverData> GameOver;
        /// <summary>그리기가 끝난 뒤 (HUD 갱신)</summary>
        public event Action<Game> Drawn;

        void Start()
        {
            m_board.Init(m_catalog);
            m_actors.Init(m_catalog, m_spriteMaterial, m_flashMaterial);
            StartGame(BattleLaunch.Current);
        }

        public void StartGame(BattleLaunch launch)
        {
            if (Game != null) Game.GameEnded -= OnGameEnded;
            Launch = launch;
            Game = new Game(launch.MakeOptions());
            Game.GameEnded += OnGameEnded;
            m_board.ResetState();
            m_actors.ResetState();
            GameStarted?.Invoke(Game);
        }

        public void Restart() => StartGame(Launch);

        void OnGameEnded(GameOverData d) => GameOver?.Invoke(d);

        void Update()
        {
            if (Game == null) return;
            BattleClock.Steps(Time.deltaTime, Waves.PlaySpeed, m_steps);
            for (int i = 0; i < m_steps.Count; i++) Game.Update(m_steps[i]);
            AnimTime = Time.time * Waves.PlaySpeed;
            Draw();
        }

        void LateUpdate()
        {
            // 캔버스 레이아웃(안전 영역·화면 회전)이 끝난 뒤 맞춘다. 모서리 4개 읽기라 매 프레임 해도 가볍다
            if (m_camera != null && m_boardArea != null) BoardSpace.FitCamera(m_camera, m_boardArea);
        }

        void Draw()
        {
            m_under.Begin();
            m_mobOver.Begin();
            m_over.Begin();
            m_top.Begin();
            m_board.Draw(Game, AnimTime, m_under);
            m_actors.Draw(Game, AnimTime, m_under, m_mobOver, m_over);
            m_under.End();
            m_mobOver.End();
            m_over.End();
            m_top.End();
            Drawn?.Invoke(Game);
        }
    }
}
