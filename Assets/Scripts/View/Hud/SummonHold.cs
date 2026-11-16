using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 소환 버튼 규칙 (ui.ts bind): 탭하면 1번, 꾹 누르고 있으면 0.38초 뒤부터 0.14초마다 골드가 떨어질 때까지.
    /// 길게 눌러 소환했으면 손을 뗄 때 들어오는 click 은 무시한다. 판이 끝나거나 일시정지·손 뗌·버튼 밖이면 멈춘다.
    /// </summary>
    public sealed class SummonHold
    {
        public const float Delay = 0.38f;
        public const float Interval = 0.14f;

        bool m_holding, m_held, m_repeating;
        float m_timer;

        public bool Holding => m_holding;
        /// <summary>방금 골드가 모자라 소환 못 함 (골드 배지 깜빡임)</summary>
        public bool GoldShort { get; private set; }
        /// <summary>마지막으로 소환된 유닛 (효과음용, 없으면 null)</summary>
        public Unit LastSummoned { get; private set; }

        public void PointerDown()
        {
            m_held = false;
            m_holding = true;
            m_repeating = false;
            m_timer = 0;
        }

        public void Release() => m_holding = false;

        /// <summary>누르고 있는 동안 실시간 dt 만큼 진행. 소환 횟수를 돌려준다</summary>
        public int Tick(float dt, Game g)
        {
            GoldShort = false;
            if (!m_holding) return 0;
            m_timer += dt;
            int n = 0;
            if (!m_repeating)
            {
                if (m_timer < Delay) return 0;
                m_held = true;
                m_repeating = true;
                m_timer = 0; // setInterval 은 첫 소환 순간부터 센다
                if (!Once(g)) { m_holding = false; return 0; }
                return 1;
            }
            while (m_holding && m_timer >= Interval)
            {
                m_timer -= Interval;
                if (g.Over || g.Paused || !Once(g)) { m_holding = false; break; }
                n++;
            }
            return n;
        }

        /// <summary>버튼 click (손을 뗌). 소환했으면 true</summary>
        public bool Click(Game g)
        {
            GoldShort = false;
            if (m_held) { m_held = false; return false; }
            return Once(g);
        }

        bool Once(Game g)
        {
            LastSummoned = null;
            if (g.Over) return false;
            Unit u = g.Summon();
            if (u == null && g.Gold < g.SummonCost) GoldShort = true;
            LastSummoned = u;
            return u != null;
        }
    }
}
