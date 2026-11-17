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
        [SerializeField] EffectsView m_effects;
        [SerializeField] FloatingTextPool m_texts;
        /// <summary>흔들림 대상 (전장 루트)</summary>
        [SerializeField] Transform m_boardRoot;
        [SerializeField] ZoneDragInput m_drag;
        /// <summary>씬이 열리면 바로 1-1 을 시작 (테스트용 - 게임에서는 끄고 로비가 고른다)</summary>
        [SerializeField] bool m_autoStart = true;

        readonly List<double> m_steps = new List<double>(8);

        public Game Game { get; private set; }
        public BattleLaunch Launch { get; private set; }
        public SpriteCatalog Catalog => m_catalog;
        public Camera Camera => m_camera;
        public Material SpriteMaterial => m_spriteMaterial;
        /// <summary>전장이 그려지는 UI 칸</summary>
        public RectTransform BoardArea => m_boardArea;
        /// <summary>지금 길 위 사거리 빈틈 수 (BoardView 가 그릴 때 센 값 - 팁 조건)</summary>
        public int GapCount => m_board != null ? m_board.GapCount : 0;
        /// <summary>애니메이션 시계 (render.ts this.t: 실제 초 × PlaySpeed)</summary>
        public double AnimTime { get; private set; }

        /// <summary>새 판이 시작됨 (HUD 등이 새 Game 에 붙는다)</summary>
        public event Action<Game> GameStarted;
        /// <summary>판을 내림 (로비로) - 화면·소리가 지난 판에서 떨어진다</summary>
        public event Action GameStopped;
        /// <summary>판 끝 (결과·부활 흐름)</summary>
        public event Action<GameOverData> GameOver;
        /// <summary>그리기가 끝난 뒤 (HUD 갱신)</summary>
        public event Action<Game> Drawn;

        void Start()
        {
            m_board.Init(m_catalog);
            m_actors.Init(m_catalog, m_spriteMaterial, m_flashMaterial);
            m_texts.Init(m_catalog.NumberFont);
            m_actors.Labels = m_texts;
            m_effects.Init(m_catalog, m_spriteMaterial, m_texts, m_boardRoot);
            if (m_autoStart) StartGame(new BattleLaunch(BattleLaunch.Mode.Stage, "1-1"));
        }

        /// <summary>판을 내린다 (로비로) - 전장은 빈 바닥만</summary>
        public void Stop()
        {
            if (Game != null) Game.GameEnded -= OnGameEnded;
            Game = null;
            GameStopped?.Invoke();
            m_board.ResetState();
            m_actors.ResetState();
            m_under.Begin(); m_under.End();
            m_mobOver.Begin(); m_mobOver.End();
            m_over.Begin(); m_over.End();
            m_top.Begin(); m_top.End();
            m_texts.BeginFrame();
            m_texts.EndFrame();
            if (m_boardRoot != null) m_boardRoot.localPosition = Vector3.zero;
        }

        public void StartGame(BattleLaunch launch)
        {
            if (Game != null) Game.GameEnded -= OnGameEnded;
            Launch = launch;
            Game = new Game(launch.MakeOptions(EconCache.State)); // 덱·레벨·연구 = 서버 경제 상태
            Game.GameEnded += OnGameEnded;
            m_board.ResetState();
            m_actors.ResetState();
            GameStarted?.Invoke(Game);
        }

        /// <summary>같은 판 다시 (부스터 없이)</summary>
        public void Restart() => StartGame(Launch.Again());

        void OnGameEnded(GameOverData d) => GameOver?.Invoke(d);

#if UNITY_EDITOR
        /// <summary>에디터 측정용: 지난 프레임 진행+그리기+HUD 에서 할당한 바이트 (에디터 자체 할당 제외)</summary>
        public static long LastFrameAlloc;
#endif

        void Update()
        {
            if (Game == null) return;
#if UNITY_EDITOR
            long before = GC.GetAllocatedBytesForCurrentThread();
#endif
            BattleClock.Steps(Time.deltaTime, Waves.PlaySpeed, m_steps);
            for (int i = 0; i < m_steps.Count; i++) Game.Update(m_steps[i]);
            AnimTime = Time.time * Waves.PlaySpeed;
            Draw();
#if UNITY_EDITOR
            LastFrameAlloc = GC.GetAllocatedBytesForCurrentThread() - before;
#endif
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
            m_texts.BeginFrame();
            ZoneDragLogic drag = m_drag != null ? m_drag.Logic : null;
            m_board.Draw(Game, AnimTime, m_under, drag);
            m_actors.Draw(Game, AnimTime, m_under, m_mobOver, m_over);
            m_effects.Draw(Game, AnimTime, m_over, m_top);
            m_board.DrawDrag(Game, m_over, drag); // 드래그 화살표는 맨 위
            m_texts.EndFrame();
            m_under.End();
            m_mobOver.End();
            m_over.End();
            m_top.End();
            Drawn?.Invoke(Game);
        }
    }
}
