using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 영웅 관리 바텀 시트 (ui.ts openHeroes): 직업 탭 / 안내(판매면 수량 1·10·ALL) / 등급 카드 6장 / 합성·판매 탭.
    /// 패널 밖(어두운 곳)이나 X 로 닫는다. 열려 있는 동안 구역 드래그를 막는다. 명단이 바뀌면 다시 그린다.
    /// </summary>
    public sealed class HeroPanel : MonoBehaviour
    {
        [SerializeField] BattleRunner m_runner;
        [SerializeField] BattleHud m_hud;
        [SerializeField] ZoneDragInput m_drag;
        [SerializeField] Toast m_toast;
        [SerializeField] BattleAudio m_audio;
        [SerializeField] GameObject m_root;
        [SerializeField] Button m_dim;
        [SerializeField] Button m_close;
        [SerializeField] HeroTab[] m_tabs = new HeroTab[0];
        [SerializeField] TextMeshProUGUI m_hint;
        [SerializeField] GameObject m_qtyRow;
        [SerializeField] Button[] m_qty = new Button[3];
        [SerializeField] GradeCardView[] m_cards = new GradeCardView[6];
        [SerializeField] Button m_mergeTab;
        [SerializeField] TextMeshProUGUI m_mergeTabText;
        [SerializeField] Button m_sellTab;

        static readonly int[] s_qtyValues = { 1, 10, HeroPanelLogic.All };
        static readonly Color32 TabOn = new Color32(0x3f, 0x8f, 0xb5, 255), TabOff = new Color32(0x7d, 0x5d, 0x42, 255);
        static readonly Color32 ReadyBorder = new Color32(0x4c, 0xaf, 0x50, 255), NormalBorder = new Color32(0x8a, 0x6f, 0x55, 255);
        static readonly Color32 TopGray = new Color32(0x88, 0x88, 0x88, 255);

        readonly HeroPanelLogic m_logic = new HeroPanelLogic();
        Game m_game;

        public bool IsOpen => m_root.activeSelf;

        void Awake()
        {
            m_runner.GameStarted += OnGameStarted;
            m_hud.HeroRequested += Open;
            m_dim.onClick.AddListener(Close);
            m_close.onClick.AddListener(Close);
            for (int i = 0; i < m_tabs.Length; i++)
            {
                int k = i;
                m_tabs[i].Button.onClick.AddListener(() => SelectTab(k));
            }
            for (int i = 0; i < m_qty.Length; i++)
            {
                int k = i;
                m_qty[i].onClick.AddListener(() => { m_logic.Qty = s_qtyValues[k]; Render(); });
            }
            for (int i = 0; i < m_cards.Length; i++)
            {
                int k = i;
                m_cards[i].Button.onClick.AddListener(() => TapGrade(k));
            }
            m_mergeTab.onClick.AddListener(() => { m_logic.MergeMode = true; Render(); });
            m_sellTab.onClick.AddListener(() => { m_logic.MergeMode = false; Render(); });
            m_root.SetActive(false);
        }

        void OnGameStarted(Game g)
        {
            if (m_game != null) m_game.RosterChanged -= OnRoster;
            m_game = g;
            g.RosterChanged += OnRoster;
            Close();
        }

        void OnRoster()
        {
            if (IsOpen) Render();
        }

        public void Open(Job? job, bool? mergeMode)
        {
            if (m_game == null || m_game.Over) return;
            m_logic.Open(m_game, job, mergeMode);
            m_root.SetActive(true);
            m_drag.Blocked = true;
            Render();
        }

        public void Close()
        {
            m_root.SetActive(false);
            if (m_drag != null) m_drag.Blocked = false;
        }

        void Update()
        {
            if (IsOpen && m_game != null && m_game.Over) Close(); // 판이 끝나면 결과 창에 자리를 내준다
        }

        void SelectTab(int i)
        {
            if (i >= m_logic.Jobs.Count) return;
            m_logic.Job = m_logic.Jobs[i];
            Render();
        }

        void TapGrade(int gi)
        {
            if (m_game == null) return;
            int before = m_game.CountOf(m_logic.Job, (GradeKey)gi);
            bool selling = !m_logic.MergeMode;
            string toast = m_logic.TapGrade(m_game, gi);
            if (toast != null) m_toast.Show(toast);
            // 판매되면 동전 소리 (ui.ts: sellMany > 0 → audio.coin). 합성 소리는 엔진 Merged 이벤트로
            if (selling && m_game.CountOf(m_logic.Job, (GradeKey)gi) < before) m_audio.Play(Sfx.Coin);
            Render();
        }

        void Render()
        {
            Game g = m_game;
            m_logic.RefreshJobs(g);
            if (!m_logic.Jobs.Contains(m_logic.Job) && m_logic.Jobs.Count > 0) m_logic.Job = m_logic.Jobs[0];
            long mask = UnitStacksView.MergeMask(g);

            for (int i = 0; i < m_tabs.Length; i++)
            {
                HeroTab tab = m_tabs[i];
                bool used = i < m_logic.Jobs.Count;
                tab.gameObject.SetActive(used);
                if (!used) continue;
                Job j = m_logic.Jobs[i];
                bool canMerge = JobCanMerge(g, j);
                tab.Label.text = Loc.Tl(Units.Get(j).Kr);
                tab.Bg.color = j == m_logic.Job ? TabOn : TabOff;
                tab.Dot.SetActive(canMerge);
            }

            bool merge = m_logic.MergeMode;
            m_hint.text = merge ? Loc.Tl("같은 카드 {0}장 → 한 등급 위로", Grades.MergeCount) : Loc.Tl("팔아서 골드 얻기");
            m_qtyRow.SetActive(!merge);
            for (int i = 0; i < m_qty.Length; i++) m_qty[i].image.color = s_qtyValues[i] == m_logic.Qty ? (Color)TabOn : (Color)TabOff;

            for (int gi = 0; gi < m_cards.Length && gi < Grades.Count; gi++)
            {
                GradeCardView c = m_cards[gi];
                Grade gr = Grades.All[gi];
                int cnt = g.CountOf(m_logic.Job, gr.Key);
                c.Name.text = Loc.Tl(gr.Kr);
                c.Name.color = Rgba.Hex(gr.Color);
                c.Icon.sprite = m_runner.Catalog.Unit(m_logic.Job, gi)[SpriteAnim.FrameIndex(0, 1)];
                c.Group.alpha = cnt == 0 ? 0.5f : 1f;
                if (merge)
                {
                    bool can = g.CanMerge(m_logic.Job, gr.Key);
                    bool isTop = gi == Grades.Count - 1;
                    c.Border.color = can ? ReadyBorder : NormalBorder;
                    c.Count.text = cnt + "/" + Grades.MergeCount;
                    c.Bottom.text = isTop ? Loc.Tl("최고") : "→" + Loc.Tl(Grades.All[gi + 1].Kr);
                    c.Bottom.color = isTop ? (Color)TopGray : (Color)Rgba.Hex(Grades.All[gi + 1].Color);
                }
                else
                {
                    int sellQty = m_logic.Qty == HeroPanelLogic.All ? cnt : Mathf.Min(m_logic.Qty, cnt);
                    c.Border.color = NormalBorder;
                    c.Count.text = cnt.ToString();
                    c.Bottom.text = "+" + sellQty * gr.SellGold;
                    c.Bottom.color = Rgba.Hex(0xa86b00);
                }
            }

            m_mergeTab.image.color = merge ? (Color)TabOn : (Color)TabOff;
            m_sellTab.image.color = merge ? (Color)TabOff : (Color)TabOn;
            m_mergeTabText.text = Loc.Icon("hammer") + " " + Loc.Tl("합성") + (mask != 0 ? " •" : "");
        }

        static bool JobCanMerge(Game g, Job j)
        {
            for (int gi = 0; gi < Grades.Count - 1; gi++)
                if (g.CanMerge(j, (GradeKey)gi)) return true;
            return false;
        }
    }
}
