using System;

namespace DefenseBattle.View
{
    /// <summary>
    /// 시트 프레임 고르기 (render.ts walkCol / attackCol). 시트 = 3열(걷기) × 4행(하/좌/우/상), 열 1 = 서 있기.
    /// </summary>
    public static class SpriteAnim
    {
        /// <summary>걷기 열 순서 (render.ts WALK_SEQ)</summary>
        static readonly int[] s_walk = { 1, 0, 1, 2 };
        /// <summary>공격 스윙 열 순서 (render.ts ATTACK_SEQ)</summary>
        static readonly int[] s_attack = { 1, 2, 0, 1 };
        /// <summary>공격 동작 시간 - 엔진 u.Flash 초기값과 같아야 함 (render.ts ATTACK_ANIM_DUR)</summary>
        public const double AttackDur = 0.1;

        public static int WalkCol(double t, double phase, double period)
        {
            long i = (long)Math.Floor((t + phase) / period) % s_walk.Length;
            return s_walk[i < 0 ? i + s_walk.Length : i];
        }

        /// <summary>flash(공격 뒤 남은 시간, AttackDur → 0)를 스윙 프레임으로</summary>
        public static int AttackCol(double flash)
        {
            double progress = 1 - Math.Max(0, Math.Min(1, flash / AttackDur));
            int idx = Math.Min(s_attack.Length - 1, (int)Math.Floor(progress * s_attack.Length));
            return s_attack[idx];
        }

        public static int FrameIndex(int dir, int col) => dir * 3 + col;
    }
}
