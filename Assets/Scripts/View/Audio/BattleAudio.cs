using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전투 소리 (audio.ts AudioManager): 효과음은 미리 만든 AudioSource 여러 개를 돌려 써서 겹쳐 울리고(할당 없음),
    /// 배경음악은 스트리밍 AudioSource 하나로 반복. 엔진과는 이벤트로만 이어진다 (Bind).
    /// 같은 소리 최소 간격·음높이 흔들기는 원본 값 (SoundRules).
    /// </summary>
    public sealed class BattleAudio : MonoBehaviour
    {
        [SerializeField] SoundCatalog m_catalog;

        /// <summary>동시에 울릴 수 있는 효과음 수</summary>
        const int Voices = 12;

        readonly SfxGate m_gate = new SfxGate();
        AudioSource[] m_voices;
        AudioSource m_bgm;
        int m_next;
        bool m_hasTrack;
        Bgm m_track;
        bool m_muted;
        Game m_game;

        public bool Muted => m_muted;

        void Awake()
        {
            m_voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) m_voices[i] = NewSource("Sfx" + i, false);
            m_bgm = NewSource("Bgm", true);
            m_muted = SoundSettings.Muted;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        // ---------------- 엔진 이벤트 ----------------
        /// <summary>새 판에 붙는다 (지난 판 이벤트는 뗀다)</summary>
        public void Bind(Game g)
        {
            Unbind();
            m_game = g;
            if (g == null) return;
            g.Shoot += OnShoot;
            g.Kill += OnKill;
            g.BossSpawned += OnBoss;
            g.WaveStarted += OnWave;
            g.Merged += OnMerged;
            g.AttackSent += OnSent;
            g.AttackIncoming += OnIncoming;
            g.GameEnded += OnEnded;
            SwitchBgm(g.IsPvp ? Bgm.Pvp : Bgm.Battle);
        }

        public void Unbind()
        {
            if (m_game == null) return;
            m_game.Shoot -= OnShoot;
            m_game.Kill -= OnKill;
            m_game.BossSpawned -= OnBoss;
            m_game.WaveStarted -= OnWave;
            m_game.Merged -= OnMerged;
            m_game.AttackSent -= OnSent;
            m_game.AttackIncoming -= OnIncoming;
            m_game.GameEnded -= OnEnded;
            m_game = null;
        }

        void OnDestroy() => Unbind();

        void OnShoot(Job job) => Play(SoundRules.AttackSfx(job));
        void OnKill(bool boss) => Play(boss ? Sfx.BossKill : Sfx.Hit);
        void OnMerged(Unit u) => Play(Sfx.Merge);
        void OnSent(AttackPayload p) => Play(Sfx.Send);
        void OnIncoming(AttackPayload p) => Play(Sfx.Incoming);

        /// <summary>보스 등장 경고 + 보스 음악 (PvP 는 음악 그대로)</summary>
        void OnBoss()
        {
            Play(Sfx.Boss);
            if (m_game != null && !m_game.IsPvp) SwitchBgm(Bgm.Boss);
        }

        /// <summary>보스 웨이브가 끝나고 다음 웨이브 → 전투 음악으로</summary>
        void OnWave(int wave)
        {
            if (m_game != null && !m_game.IsPvp && !m_game.IsBossWave) SwitchBgm(Bgm.Battle);
        }

        void OnEnded(GameOverData d)
        {
            StopBgm();
            Play(d.Cleared || d.PvpWon ? Sfx.Win : Sfx.Lose);
        }

        // ---------------- 효과음 ----------------
        public void Play(Sfx s)
        {
            float vol = SoundSettings.SfxVolume;
            if (m_muted || vol <= 0 || m_voices == null) return;
            AudioClip clip = m_catalog.Clip(s);
            if (clip == null) return;
            if (!m_gate.Allow(s, Time.unscaledTimeAsDouble * 1000.0, SoundRules.MinGapMs(s))) return;
            AudioSource src = m_voices[m_next];
            m_next = (m_next + 1) % m_voices.Length;
            SoundRules.PitchRange(s, out float min, out float max);
            src.pitch = min == max ? 1f : Random.Range(min, max);
            src.volume = SoundRules.SfxVolume * vol;
            src.clip = clip;
            src.Play();
        }

        // ---------------- 배경음악 ----------------
        /// <summary>같은 곡이면 그대로, 다르면 바꿔 처음부터 (audio.ts switchBgm)</summary>
        public void SwitchBgm(Bgm track)
        {
            if (m_bgm == null) return;
            if (m_hasTrack && m_track == track && m_bgm.clip != null) return;
            m_hasTrack = true;
            m_track = track;
            m_bgm.Stop();
            m_bgm.clip = m_catalog.Track(track);
            m_bgm.volume = SoundRules.BgmVolume * SoundSettings.BgmVolume;
            if (!m_muted && SoundSettings.BgmVolume > 0 && m_bgm.clip != null) m_bgm.Play();
        }

        /// <summary>설정 음량 바꾸기 (audio.ts setBgmVolume): 0 이면 멈추고, 0 에서 올리면 다시 튼다 (저장)</summary>
        public void SetBgmVolume(float v)
        {
            SoundSettings.BgmVolume = v;
            if (m_bgm == null) return;
            m_bgm.volume = SoundRules.BgmVolume * SoundSettings.BgmVolume;
            if (SoundSettings.BgmVolume <= 0) m_bgm.Pause();
            else if (!m_muted && m_hasTrack && m_bgm.clip != null && !m_bgm.isPlaying)
            {
                if (m_bgm.time > 0) m_bgm.UnPause();
                else m_bgm.Play();
            }
        }

        public void SetSfxVolume(float v) => SoundSettings.SfxVolume = v;

        public void StopBgm()
        {
            if (m_bgm == null) return;
            m_bgm.Stop();
            m_hasTrack = false;
        }

        /// <summary>스피커 버튼: 효과음·음악 모두 (저장)</summary>
        public bool ToggleMute()
        {
            m_muted = !m_muted;
            SoundSettings.Muted = m_muted;
            if (m_muted)
            {
                m_bgm.Pause();
                foreach (AudioSource v in m_voices) v.Stop();
            }
            else if (m_hasTrack && m_bgm.clip != null)
            {
                if (m_bgm.time > 0) m_bgm.UnPause();
                else m_bgm.Play();
            }
            return m_muted;
        }
    }
}
