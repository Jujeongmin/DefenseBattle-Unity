using System;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 상대가 보낸 몹 검사. 원본은 서버가 웨이브를 1~999 로 잘랐지만(server.ts pvpAttack) 이제 기기끼리 직접 주고받으므로 받는 쪽이 막는다:
    /// 웨이브는 1 ~ 내 웨이브 + WaveSlack (같은 시드·같은 시각에 시작해 웨이브가 거의 같다 - 보스 웨이브 차이만큼 여유),
    /// 1초에 MaxPerSecond 개까지 (카드마다 쿨타임이 있어 사람 손으로는 넘지 않는다). 카드 번호·위치는 Game.ReceiveAttack 이 고친다.
    /// </summary>
    public sealed class AttackGate
    {
        public const int MaxPerSecond = 3;
        public const int WaveSlack = 5;

        double m_windowStart = double.NegativeInfinity;
        int m_count;

        /// <param name="now">대전 시계 (초)</param>
        public bool Allow(ref AttackPayload p, int myWave, double now)
        {
            if (now - m_windowStart >= 1)
            {
                m_windowStart = now;
                m_count = 0;
            }
            if (m_count >= MaxPerSecond) return false;
            m_count++;
            p.Wave = Math.Max(1, Math.Min(p.Wave, Math.Max(1, myWave) + WaveSlack));
            return true;
        }
    }
}
