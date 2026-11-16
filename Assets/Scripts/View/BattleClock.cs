using System;
using System.Collections.Generic;

namespace DefenseBattle.View
{
    /// <summary>
    /// 실제 경과 시간 → 엔진 Update 조각들 (main.ts step).
    /// 실제 dt 를 0.1초로 자르고(긴 멈춤 뒤 순간이동·대량 스폰 방지) × PlaySpeed, 0.05 단위로 나눈다 (충돌·이동 정확도).
    /// </summary>
    public static class BattleClock
    {
        /// <summary>한 프레임 최대 실제 시간 (main.ts: 일반 0.1)</summary>
        public const float MaxRealDt = 0.1f;
        /// <summary>엔진 한 조각 최대 (main.ts Math.min(dt, 0.05))</summary>
        public const double MaxStep = 0.05;

        public static void Steps(float realDt, double playSpeed, List<double> outSteps)
        {
            outSteps.Clear();
            if (realDt <= 0) return;
            double dt = Math.Min(realDt, MaxRealDt) * playSpeed;
            // float 0.1 의 오차(1e-9 수준)로 생기는 의미 없는 조각은 버린다
            while (dt > 1e-6)
            {
                double d = Math.Min(dt, MaxStep);
                outSteps.Add(d);
                dt -= d;
            }
        }
    }
}
