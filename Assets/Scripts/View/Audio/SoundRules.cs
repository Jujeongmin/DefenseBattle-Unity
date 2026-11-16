using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 소리 규칙 (audio.ts). 파일 음량은 원본 도구로 맞춰져 있어 효과음마다 볼륨을 따로 주지 않는다.
    /// </summary>
    public static class SoundRules
    {
        /// <summary>모든 효과음 공통 음량 (audio.ts SFX_VOL - 겹칠 때 여유)</summary>
        public const float SfxVolume = 0.8f;
        /// <summary>배경음악 음량 (audio.ts BGM_VOL - 효과음보다 약 7dB 아래)</summary>
        public const float BgmVolume = 0.45f;

        /// <summary>직업별 공격음 (ui.ts game.on('shoot')): 원본은 직업 이름으로만 나눈다</summary>
        public static Sfx AttackSfx(Job job)
        {
            if (job == Job.Archer || job == Job.Ranger) return Sfx.Arrow;
            if (job == Job.Wizard || job == Job.Frost || job == Job.Bomber) return Sfx.Magic;
            return Sfx.Sword;
        }

        /// <summary>같은 소리가 너무 촘촘히 겹치지 않게 하는 최소 간격 (ms, audio.ts minGap)</summary>
        public static int MinGapMs(Sfx s)
        {
            switch (s)
            {
                case Sfx.Button: return 40;
                case Sfx.Coin: return 60;
                case Sfx.Summon:
                case Sfx.Mythic: return 50;
                case Sfx.Arrow: return 90;
                case Sfx.Magic: return 140;
                case Sfx.Sword: return 110;
                case Sfx.Hit: return 60;
                case Sfx.Send: return 100;
                case Sfx.Incoming: return 300;
                default: return 0;
            }
        }

        /// <summary>음높이를 흔드는 폭 (공격음 0.92~1.08, 처치음 0.9~1.1 - 단조로움 방지). 없으면 0</summary>
        public static void PitchRange(Sfx s, out float min, out float max)
        {
            switch (s)
            {
                case Sfx.Arrow:
                case Sfx.Magic:
                case Sfx.Sword: min = 0.92f; max = 1.08f; return;
                case Sfx.Hit: min = 0.9f; max = 1.1f; return;
                default: min = max = 1f; return;
            }
        }
    }
}
