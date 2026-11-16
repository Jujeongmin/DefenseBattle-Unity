namespace DefenseBattle.View
{
    /// <summary>같은 효과음 최소 간격 판정 (audio.ts lastPlayed) - 할당 없이 종류별 배열</summary>
    public sealed class SfxGate
    {
        readonly double[] m_last = new double[16];

        public SfxGate()
        {
            for (int i = 0; i < m_last.Length; i++) m_last[i] = double.NegativeInfinity;
        }

        /// <summary>지금 울려도 되면 true (울린 시각을 기록한다)</summary>
        public bool Allow(Sfx s, double nowMs, double minGapMs)
        {
            int i = (int)s;
            if (minGapMs > 0 && nowMs - m_last[i] < minGapMs) return false;
            m_last[i] = nowMs;
            return true;
        }
    }
}
