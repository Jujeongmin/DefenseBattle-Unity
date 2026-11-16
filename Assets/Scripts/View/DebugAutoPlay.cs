#if UNITY_EDITOR
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 에디터 전용 자동 플레이 (끝까지 플레이 확인용): 봇 AI(BotPlayer)가 소환·합성·강화·구역 교환을 하고 Time.timeScale 로 빨리 돌린다.
    /// 플레이 모드에서 Runner 에 붙여 쓴다.
    /// </summary>
    public sealed class DebugAutoPlay : MonoBehaviour
    {
        public double Skill = 0.85;
        public float TimeScale = 4f;

        BattleRunner m_runner;
        BotPlayer m_bot;
        Game m_game;
        double m_acc;

        /// <summary>프레임당 GC 할당 (에디터 Profiler 카운터) - 10초마다 로그</summary>
        Unity.Profiling.ProfilerRecorder m_gc;
        long m_gcMax, m_gcSum, m_gcFrames, m_gcNonZero;
        float m_logAt;

        void OnEnable()
        {
            m_runner = GetComponent<BattleRunner>();
            Time.timeScale = TimeScale;
            m_gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            m_logAt = Time.unscaledTime + 10;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            m_gc.Dispose();
        }

        void LateUpdate()
        {
            long v = BattleRunner.LastFrameAlloc; // 우리 코드만 (Profiler 카운터는 에디터 할당이 섞인다)
            m_gcFrames++;
            m_gcSum += v;
            if (v > 0) m_gcNonZero++;
            if (v > m_gcMax) m_gcMax = v;
            if (Time.unscaledTime < m_logAt) return;
            m_logAt = Time.unscaledTime + 10;
            Game g = m_runner.Game;
            Debug.Log($"[AutoPlay] wave {(g != null ? g.Wave : 0)} mobs {(g != null ? g.Mobs.Count : 0)} | GC/frame avg {m_gcSum / Mathf.Max(1, m_gcFrames)}B max {m_gcMax}B, frames with alloc {m_gcNonZero}/{m_gcFrames}");
            m_gcMax = m_gcSum = m_gcFrames = m_gcNonZero = 0;
        }

        void Update()
        {
            Game g = m_runner != null ? m_runner.Game : null;
            if (g == null || g.Over) return;
            if (g != m_game)
            {
                m_game = g;
                m_bot = new BotPlayer(g, Skill);
            }
            // 봇 판단은 실시간 기준 (BotMatch 와 같은 손 속도)
            m_acc += Time.deltaTime;
            if (m_acc < BotPlayer.ThinkInterval(Skill)) return;
            m_acc = 0;
            m_bot.Step();
        }
    }
}
#endif
