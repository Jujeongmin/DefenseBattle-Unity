using System;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 하단 덱 카드 (ui.ts renderStacks): 수량·최고 등급 테두리·상성 ▲▼·이름 Lv·강화 버튼, 합성 가능하면 '합성!'.
    /// 카드 내용은 명단·종족·합성 가능 목록이 바뀔 때만 다시 쓰고, 강화 버튼 활성만 매 프레임 본다.
    /// </summary>
    public sealed class UnitStacksView : MonoBehaviour
    {
        [SerializeField] StackCard[] m_cards = new StackCard[0];

        static readonly Color32 EmptyBorder = new Color32(0x8a, 0x6f, 0x55, 255);
        static readonly Color32 Up = new Color32(0x4c, 0xaf, 0x50, 255), Down = new Color32(0xe5, 0x48, 0x4d, 255);

        Game m_game;
        SpriteCatalog m_catalog;
        bool m_dirty = true;
        readonly int[] m_count = new int[4];
        readonly int[] m_cost = new int[4];

        /// <summary>합성 가능한 카드를 누름 (직업)</summary>
        public event Action<Job> MergeRequested;
        /// <summary>강화함</summary>
        public event Action Upgraded;

        void Awake()
        {
            for (int i = 0; i < m_cards.Length; i++)
            {
                int slot = i; // 버튼마다 한 번 (판 중 할당 아님)
                m_cards[i].UpButton.onClick.AddListener(() => OnUpgrade(slot));
                m_cards[i].CardButton.onClick.AddListener(() => OnCard(slot));
            }
        }

        public void Bind(Game g, SpriteCatalog catalog)
        {
            if (m_game != null) m_game.RosterChanged -= MarkDirty;
            m_game = g;
            m_catalog = catalog;
            g.RosterChanged += MarkDirty;
            m_dirty = true;
        }

        public void MarkDirty() => m_dirty = true;

        /// <summary>매 프레임 (mergeMask = 합성 가능 비트: 덱 칸*6 + 등급)</summary>
        public void Refresh(Game g, long mergeMask)
        {
            if (m_dirty) Rebuild(g, mergeMask);
            for (int i = 0; i < m_cards.Length; i++)
            {
                if (i >= g.Opts.Deck.Count) continue;
                bool can = m_count[i] > 0 && g.Gold >= m_cost[i];
                if (m_cards[i].UpButton.interactable != can) m_cards[i].UpButton.interactable = can;
            }
        }

        void Rebuild(Game g, long mergeMask)
        {
            m_dirty = false;
            int shown = Math.Min(g.Opts.Deck.Count, m_cards.Length);
            for (int i = 0; i < m_cards.Length; i++)
            {
                StackCard card = m_cards[i];
                bool used = i < shown;
                if (card.gameObject.activeSelf != used) card.gameObject.SetActive(used);
                if (!used) continue; // 배치는 HorizontalLayoutGroup (가운데 정렬, 폭에 맞춰 줄어듦)
                Job job = g.Opts.Deck[i];
                int count = 0, top = 0;
                foreach (Unit u in g.Units)
                {
                    if (u.Job != job) continue;
                    count++;
                    if (u.GradeIndex > top) top = u.GradeIndex;
                }
                m_count[i] = count;
                m_cost[i] = g.UpgradeCost(job);
                bool merge = (mergeMask & (0x3FL << (i * 6))) != 0;
                bool strong = Units.DmgMult(job, g.CurrentRace) >= 1; // 상성: 유리 ▲ / 불리 ▼

                card.Group.alpha = count > 0 ? 1f : 0.55f;
                card.Border.color = count > 0 ? (Color)Rgba.Hex(Grades.All[top].Color) : (Color)EmptyBorder;
                card.Count.text = count.ToString();
                card.MergeTag.SetActive(merge);
                card.Arrow.sprite = m_catalog.Icon(strong ? "up" : "down");
                card.Arrow.color = strong ? Up : Down;
                card.Icon.sprite = m_catalog.Unit(job, top)[SpriteAnim.FrameIndex(0, 1)];
                card.Name.text = Loc.Tl(Units.Get(job).Kr) + " Lv." + g.LevelOf(job);
                card.UpText.text = Loc.Tl("강화") + " " + Loc.Icon("coin") + m_cost[i];
            }
        }

        void OnUpgrade(int slot)
        {
            if (m_game == null || slot >= m_game.Opts.Deck.Count) return;
            if (m_game.Upgrade(m_game.Opts.Deck[slot]))
            {
                m_dirty = true;
                Upgraded?.Invoke();
            }
        }

        void OnCard(int slot)
        {
            if (m_game == null || slot >= m_game.Opts.Deck.Count) return;
            Job job = m_game.Opts.Deck[slot];
            for (int gi = 0; gi < Grades.Count; gi++)
                if (m_game.CanMerge(job, (GradeKey)gi)) { MergeRequested?.Invoke(job); return; }
        }

        /// <summary>합성 가능 비트 (덱 칸*6 + 등급) - Mergeable() 의 리스트 할당 없이</summary>
        public static long MergeMask(Game g)
        {
            long mask = 0;
            for (int i = 0; i < g.Opts.Deck.Count && i < 10; i++)
                for (int gi = 0; gi < Grades.Count - 1; gi++)
                    if (g.CanMerge(g.Opts.Deck[i], (GradeKey)gi)) mask |= 1L << (i * 6 + gi);
            return mask;
        }
    }
}
