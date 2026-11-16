using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>효과음·배경음악 목록 (Build Battle Assets 메뉴가 채운다). 인덱스 = Sfx / Bgm 순서</summary>
    public sealed class SoundCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/Audio/DefenseBattle/SoundCatalog.asset";

        [SerializeField] AudioClip[] m_sfx = new AudioClip[0];
        [SerializeField] AudioClip[] m_bgm = new AudioClip[0];

        public AudioClip Clip(Sfx s) => (int)s < m_sfx.Length ? m_sfx[(int)s] : null;
        public AudioClip Track(Bgm b) => (int)b < m_bgm.Length ? m_bgm[(int)b] : null;

#if UNITY_EDITOR
        public void EditorFill(AudioClip[] sfx, AudioClip[] bgm)
        {
            m_sfx = sfx;
            m_bgm = bgm;
        }
#endif
    }
}
