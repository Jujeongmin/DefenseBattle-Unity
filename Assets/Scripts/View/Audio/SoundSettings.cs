using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 소리 설정 저장 (audio.ts MUTE_KEY / BGM_VOL_KEY / SFX_VOL_KEY). 음량 조절 화면은 3단계 설정에서.
    /// </summary>
    public static class SoundSettings
    {
        public const string MuteKey = "audio.muted";
        public const string BgmVolumeKey = "audio.bgmVolume";
        public const string SfxVolumeKey = "audio.sfxVolume";

        /// <summary>전투 화면 스피커 버튼 (전체)</summary>
        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>배경음악 음량 0~1 (0 = 끔)</summary>
        public static float BgmVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            set => PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        }

        /// <summary>효과음 음량 0~1 (0 = 끔)</summary>
        public static float SfxVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }
    }
}
