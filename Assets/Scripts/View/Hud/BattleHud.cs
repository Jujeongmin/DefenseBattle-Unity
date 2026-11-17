using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전투 HUD (ui.ts update / bind): 상단바(몹 초상화·종족·스테이지·웨이브·보스 시간·소리·일시정지·배속),
    /// 전장 위(소환 로그·골드 배지·신화 배너), 진행 바(필드 몹 n/100), 하단(영웅·소환·나가기·덱 카드).
    /// BattleRunner.Drawn 마다 Refresh - 글자는 값이 바뀐 때만 넣는다.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] BattleRunner m_runner;
        [SerializeField] BattleAudio m_audio;
        [SerializeField] Toast m_toast;
        [Header("상단바")]
        [SerializeField] Image m_portrait;
        [SerializeField] TextMeshProUGUI m_race;
        [SerializeField] TextMeshProUGUI m_stage;
        [SerializeField] TextMeshProUGUI m_wave;
        [SerializeField] TextMeshProUGUI m_bossTimer;
        [SerializeField] Button m_muteButton;
        [SerializeField] Image m_muteIcon;
        [SerializeField] Button m_pauseButton;
        [SerializeField] Image m_pauseIcon;
        [SerializeField] Button m_speedButton;
        [SerializeField] TextMeshProUGUI m_speedText;
        [Header("전장 위")]
        [SerializeField] RectTransform m_goldBadge;
        [SerializeField] TextMeshProUGUI m_gold;
        [SerializeField] TextMeshProUGUI m_summonLog;
        [SerializeField] GameObject m_mythic;
        [SerializeField] TextMeshProUGUI m_mythicText;
        [Header("진행 바")]
        [SerializeField] RectTransform m_progressFill;
        [SerializeField] Image m_progressFillImage;
        [SerializeField] TextMeshProUGUI m_progressLabel;
        [Header("하단")]
        [SerializeField] SummonButton m_summon;
        [SerializeField] CanvasGroup m_summonGroup;
        [SerializeField] TextMeshProUGUI m_summonCost;
        [SerializeField] Button m_heroButton;
        [SerializeField] GameObject m_mergeDot;
        [SerializeField] Button m_exitButton;
        [SerializeField] UnitStacksView m_stacks;

        const string SpeedKey = "battle.speed";
        /// <summary>배속 (ui.ts speeds): 3배속은 광고 제거 패키지가 있을 때만</summary>
        static readonly double[] s_speeds2 = { 1, 2 }, s_speeds3 = { 1, 2, 3 };
        double[] m_speeds = s_speeds2;
        static readonly Color32 Red = new Color32(0xe5, 0x48, 0x4d, 255), RedBright = new Color32(0xff, 0x6c, 0x73, 255);
        static readonly Color32 Gold = new Color32(0xff, 0xc9, 0x28, 255), GoldShortColor = new Color32(0xff, 0x52, 0x52, 255);

        Game m_game;
        int m_speedIdx;
        Race m_lastRace = (Race)(-1);
        long m_mergeMask = -1;
        float m_progress = -1;
        bool m_progressBright;
        int m_logCount = -1;
        LogEntry m_logFirst, m_logLast;
        float m_goldFlash;
        readonly char[] m_buf = new char[512];
        Button m_summonButton;
        CachedText m_waveText, m_goldText, m_progressText, m_bossText, m_costText, m_logText, m_mythicCached;

        /// <summary>영웅 관리 열기 (직업, 합성 모드) - 영웅 버튼은 둘 다 null (패널이 고른다)</summary>
        public event Action<Job?, bool?> HeroRequested;
        /// <summary>나가기</summary>
        public event Action ExitRequested;

        void Awake()
        {
            m_summonButton = m_summon.GetComponent<Button>();
            m_waveText = new CachedText(m_wave);
            m_goldText = new CachedText(m_gold);
            m_progressText = new CachedText(m_progressLabel);
            m_bossText = new CachedText(m_bossTimer);
            m_costText = new CachedText(m_summonCost);
            m_logText = new CachedText(m_summonLog);
            m_mythicCached = new CachedText(m_mythicText);
            m_runner.GameStarted += OnGameStarted;
            m_runner.Drawn += Refresh;
            m_pauseButton.onClick.AddListener(OnPause);
            m_speedButton.onClick.AddListener(OnSpeed);
            m_muteButton.onClick.AddListener(OnMute);
            m_heroButton.onClick.AddListener(() => { m_audio.Play(Sfx.Button); HeroRequested?.Invoke(null, null); });
            m_exitButton.onClick.AddListener(() => { m_audio.Play(Sfx.Button); ExitRequested?.Invoke(); });
            m_summon.GoldShort += () => m_goldFlash = 0.15f;
            // 소환음: 신화 이상이면 팡파르 (ui.ts audio.summon(gradeIndex >= mythic))
            m_summon.Summoned += u => m_audio.Play(u.GradeIndex >= (int)GradeKey.Mythic ? Sfx.Mythic : Sfx.Summon);
            m_stacks.MergeRequested += job => HeroRequested?.Invoke(job, true);
            m_stacks.Upgraded += () => m_audio.Play(Sfx.Button);
            m_runner.GameStopped += OnGameStopped;
            SetMuteIcon();
        }

        /// <summary>판이 내려감: 지난 판을 놓는다 (리뷰: Stop 뒤에도 옛 Game 을 들고 있던 문제)</summary>
        void OnGameStopped()
        {
            HideCoach();
            m_game = null;
            m_summon.Game = null;
        }

        void OnDestroy()
        {
            if (m_runner == null) return;
            m_runner.GameStarted -= OnGameStarted;
            m_runner.Drawn -= Refresh;
            m_runner.GameStopped -= OnGameStopped;
        }

        void OnGameStarted(Game g)
        {
            HideCoach();
            m_game = g;
            m_summon.Game = g;
            m_stacks.Bind(g, m_runner.Catalog);
            // 배속은 판이 바뀌어도 마지막으로 고른 값 (쓸 수 없는 배속이면 1배속)
            double saved = PlayerPrefs.GetFloat(SpeedKey, 1);
            m_speeds = EconCache.State.AdRemoved ? s_speeds3 : s_speeds2;
            m_speedIdx = Math.Max(0, Array.IndexOf(m_speeds, saved));
            g.Speed = m_speeds[m_speedIdx];
            m_speedText.text = g.Speed + "x";
            m_pauseIcon.sprite = m_runner.Catalog.Icon("pause");
            BattleLaunch l = m_runner.Launch;
            m_stage.text = l.Kind == BattleLaunch.Mode.Stage ? l.StageId : Loc.Tl("무한");
            m_lastRace = (Race)(-1);
            m_mergeMask = -1;
            m_progress = -1;
            m_logCount = -1;
        }

        void Refresh(Game g)
        {
            m_waveText.Set(m_buf, WaveLabel(g, m_buf));

            if (m_lastRace != g.CurrentRace)
            {
                m_lastRace = g.CurrentRace;
                m_race.text = Loc.Tl(Races.Kr[(int)g.CurrentRace]);
                m_portrait.sprite = m_runner.Catalog.Mob(g.CurrentRace, 0)[SpriteAnim.FrameIndex(0, 1)];
                m_stacks.MarkDirty(); // 상성 화살표
            }

            m_goldText.Set(m_buf, CharText.PutInt(m_buf, 0, g.Gold));

            int mobs = g.Mobs.Count;
            float pct = Mathf.Min(1f, (float)mobs / Waves.GameOverMobCount);
            if (pct != m_progress)
            {
                m_progress = pct;
                m_progressFill.anchorMax = new Vector2(pct, 1);
            }
            // 70% 이상: 0.6초 주기로 밝게 깜빡 (style.css warnBlink)
            bool bright = pct >= 0.7f && Time.unscaledTime % 0.6f < 0.3f;
            if (bright != m_progressBright)
            {
                m_progressBright = bright;
                m_progressFillImage.color = bright ? RedBright : Red;
            }
            int n = CharText.PutInt(m_buf, 0, mobs);
            n = CharText.Put(m_buf, n, " / ");
            m_progressText.Set(m_buf, CharText.PutInt(m_buf, n, Waves.GameOverMobCount));

            bool boss = g.BossTimer > 0; // 살아 있는 보스의 남은 제한 시간
            if (m_bossTimer.gameObject.activeSelf != boss) m_bossTimer.gameObject.SetActive(boss);
            if (boss)
            {
                n = CharText.Put(m_buf, 0, "<sprite name=\"timer\"> ");
                n = CharText.PutFixed1(m_buf, n, g.BossTimer / Waves.PlaySpeed);
                m_bossText.Set(m_buf, CharText.Put(m_buf, n, 's'));
            }

            RefreshLog(g);
            bool banner = g.MythicBanner != null;
            if (m_mythic.activeSelf != banner) m_mythic.SetActive(banner);
            if (banner) m_mythicCached.Set(g.MythicBanner.Text);

            int cost = g.SummonCost;
            bool canSummon = g.Gold >= cost && !g.Over;
            if (m_summonButton.interactable != canSummon)
            {
                m_summonButton.interactable = canSummon;
                m_summonGroup.alpha = canSummon ? 1f : 0.65f;
            }
            n = CharText.Put(m_buf, 0, "<sprite name=\"coin\">");
            m_costText.Set(m_buf, CharText.PutInt(m_buf, n, cost));

            long mask = UnitStacksView.MergeMask(g);
            if (mask != m_mergeMask)
            {
                m_mergeMask = mask;
                m_mergeDot.SetActive(mask != 0);
                m_stacks.MarkDirty();
            }
            m_stacks.Refresh(g, mask);
            MaybeTip(g);
        }

        /// <summary>소환 로그 (최근 6줄) - 줄이 바뀐 때만</summary>
        void RefreshLog(Game g)
        {
            LogEntry first = g.Log.Count > 0 ? g.Log[0] : null, last = g.Log.Count > 0 ? g.Log[g.Log.Count - 1] : null;
            if (g.Log.Count == m_logCount && first == m_logFirst && last == m_logLast) return;
            m_logCount = g.Log.Count;
            m_logFirst = first;
            m_logLast = last;
            int n = 0;
            // 원본은 위에서부터 최신 순 (unshift) - 아래 정렬 칸이라 오래된 줄이 위로
            for (int i = 0; i < g.Log.Count; i++)
            {
                if (i > 0) n = CharText.Put(m_buf, n, '\n');
                n = CharText.Put(m_buf, n, g.Log[i].Text);
            }
            m_logText.Set(m_buf, n);
        }

        // ---------- 튜토리얼 대상 ----------
        public GameObject SummonTarget => m_summon.gameObject;
        public GameObject HeroTarget => m_heroButton.gameObject;
        public GameObject UpgradeTarget() => m_stacks.FirstUpgrade();

        /// <summary>튜토리얼 판: 나가기 버튼을 숨긴다 (자리는 남겨 하단 배치가 그대로 - ui.ts visibility hidden)</summary>
        public void SetExitHidden(bool hidden)
        {
            if (m_exitGroup == null) m_exitGroup = m_exitButton.gameObject.AddComponent<CanvasGroup>();
            m_exitGroup.alpha = hidden ? 0 : 1;
            m_exitGroup.interactable = m_exitGroup.blocksRaycasts = !hidden;
        }

        // ---------- 판 안 한 번씩 나오는 안내 (ui.ts maybeTip · coach) ----------
        /// <summary>튜토리얼 스포트라이트가 떠 있는 동안 등 팁을 막는다</summary>
        public bool TipsBlocked { get; set; }

        void MaybeTip(Game g)
        {
            if (m_coachLeft > 0 || g.Over || g.Paused || TipsBlocked) return;
            string tip = GuideRules.PickTip(m_runner.GapCount, g.Units.Count, g.Time, g.IsPvp, g.Wave, LocalMeta.Data.SeenFeatures);
            if (tip == null) return;
            LocalMeta.MarkSeen("tip:" + tip);
            string text;
            switch (tip)
            {
                case GuideRules.TipGap:
                    text = Loc.Icon("warning") + " " + Loc.Tl("길의 <b>빨간 곳</b>은 유닛 공격이 닿지 않는 빈틈이에요.<br>유닛을 끌어 옮겨서 빈틈을 막아요");
                    break;
                case GuideRules.TipPeek:
                    text = Loc.Tl("상대 전장을 <b>누르고 있으면</b>(PC는 마우스를 올리면)<br>크게 볼 수 있어요");
                    break;
                default:
                    text = Loc.Tl("웨이브가 지날수록 몹 체력이 크게 늘어요.<br>후반엔 소환보다 <b>몹 카드로 상대를 압박</b>하는 게 효율적이에요");
                    break;
            }
            ShowCoach(text, 6.5f);
        }

        /// <summary>전장 아래쪽 안내 말풍선 (처음 쓸 때 만든다)</summary>
        public void ShowCoach(string text, float seconds)
        {
            if (m_coach == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                Image bg = Ui.Frame(canvas.transform, "paper", 1.4f, "Coach");
                m_coach = bg.rectTransform;
                m_coach.anchorMin = m_coach.anchorMax = new Vector2(0.5f, 0.5f);
                m_coach.pivot = new Vector2(0.5f, 0);
                Ui.Column(bg.transform, 0, 26, 26, 18, 20, TextAnchor.MiddleCenter);
                bg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                m_coachText = Ui.Text(bg.transform, "", 26, Ui.Ink, TextAlignmentOptions.Center, true);
            }
            // 전장 아래쪽 가운데, 전장 너비의 90%
            RectTransform board = m_runner.BoardArea;
            var parent = (RectTransform)m_coach.parent;
            Vector3[] c = s_corners;
            board.GetWorldCorners(c);
            Vector3 bl = parent.InverseTransformPoint(c[0]), br = parent.InverseTransformPoint(c[3]);
            m_coach.anchoredPosition = new Vector2((bl.x + br.x) * 0.5f, bl.y + 16);
            m_coach.sizeDelta = new Vector2((br.x - bl.x) * 0.9f, m_coach.sizeDelta.y);
            m_coach.SetAsLastSibling();
            m_coachText.text = text;
            m_coach.gameObject.SetActive(true);
            m_coachLeft = seconds;
        }

        public void HideCoach()
        {
            m_coachLeft = 0;
            if (m_coach != null) m_coach.gameObject.SetActive(false);
        }

        static readonly Vector3[] s_corners = new Vector3[4];
        RectTransform m_coach;
        TextMeshProUGUI m_coachText;
        float m_coachLeft;
        CanvasGroup m_exitGroup;

        void Update()
        {
            if (m_coachLeft > 0)
            {
                m_coachLeft -= Time.unscaledDeltaTime;
                if (m_coachLeft <= 0) HideCoach();
            }
            if (m_goldFlash <= 0) return;
            m_goldFlash -= Time.unscaledDeltaTime;
            bool on = m_goldFlash > 0;
            m_goldBadge.localScale = on ? new Vector3(1.3f, 1.3f, 1) : Vector3.one;
            m_gold.color = on ? (Color)GoldShortColor : (Color)Gold;
        }

        void OnPause()
        {
            if (m_game == null) return;
            m_audio.Play(Sfx.Button);
            m_game.Paused = !m_game.Paused;
            m_pauseIcon.sprite = m_runner.Catalog.Icon(m_game.Paused ? "play" : "pause");
        }

        void OnSpeed()
        {
            if (m_game == null) return;
            m_audio.Play(Sfx.Button);
            if (m_speeds.Length == 2 && m_speedIdx == 1 && m_toast != null) m_toast.Show(Loc.Tl("3배속은 광고 제거 패키지로 해금돼요"));
            m_speedIdx = (m_speedIdx + 1) % m_speeds.Length;
            m_game.Speed = m_speeds[m_speedIdx];
            m_speedText.text = m_game.Speed + "x";
            PlayerPrefs.SetFloat(SpeedKey, (float)m_game.Speed);
        }

        /// <summary>스피커 버튼: 효과음·음악 모두 끄고 켬 (저장 - audio.ts toggleMute)</summary>
        void OnMute()
        {
            m_audio.ToggleMute();
            SetMuteIcon();
        }

        void SetMuteIcon()
        {
            if (m_runner != null && m_runner.Catalog != null && m_muteIcon != null)
                m_muteIcon.sprite = m_runner.Catalog.Icon(SoundSettings.Muted ? "mute" : "sound");
        }

        /// <summary>"Wave 3/10" (PvP 처럼 끝이 없으면 "Wave 3"), 시작 전에도 1 (ui.ts update)</summary>
        public static int WaveLabel(Game g, char[] buf)
        {
            int total = g.TotalWaves;
            int n = CharText.Put(buf, 0, "Wave ");
            if (total == int.MaxValue) return CharText.PutInt(buf, n, Math.Max(1, g.Wave));
            n = CharText.PutInt(buf, n, Math.Max(1, Math.Min(g.Wave, total)));
            n = CharText.Put(buf, n, '/');
            return CharText.PutInt(buf, n, total);
        }
    }
}
