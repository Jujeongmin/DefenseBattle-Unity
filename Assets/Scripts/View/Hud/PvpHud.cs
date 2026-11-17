using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 대전 HUD (index.html #pvp-hud · #attack-row, ui.ts setOpponentInfo · setOpponent · buildAttackCards · countdown).
    /// 처음 대전할 때 코드로 만들고, 대전 동안만 보인다. 보이는 동안 HUD 배치를 바꾼다:
    /// 상단 바 아래 'VS 줄'(내 이름 vs 상대, 필드 몹 막대 둘, 상대 카드, 경과 시간, 상대 미니맵 자리),
    /// 조작부 위 '몹 카드 줄'(4장, 비용·쿨타임). 전장 가운데 시작 카운트다운.
    /// 매 프레임 갱신(Tick)은 값이 바뀐 글자만 쓴다 (할당 없음).
    /// </summary>
    public sealed class PvpHud
    {
        public const float VsHeight = 124, CardsHeight = 112;
        static readonly Color32 BarBg = new Color32(0, 0, 0, 140), MeFill = new Color32(0x4f, 0x9d, 0xff, 255), OppFill = new Color32(0xe5, 0x48, 0x4d, 255),
            WarnFill = new Color32(0xff, 0x6c, 0x73, 255), Strip = new Color32(0x2a, 0x1d, 0x16, 235);

        readonly RectTransform m_safe, m_top, m_board, m_progress, m_controls;
        readonly SpriteCatalog m_cat;
        readonly char[] m_buf = new char[64];
        float m_boardBottom, m_progressBottom, m_progressTop, m_controlsTop;

        RectTransform m_vs, m_cards, m_countRoot;
        RectTransform m_meFill, m_oppFill;
        Image m_oppFillImage;
        CachedText m_meCount, m_oppCount, m_clock, m_count, m_oppNick, m_myNick, m_oppSub;
        TextMeshProUGUI m_countSub;
        RectTransform m_oppDeck;
        Image[] m_oppDeckIcons;
        readonly int[] m_shownAtk = new int[OppFrame.MaxDeck], m_shownDeck = new int[OppFrame.MaxDeck];
        int m_shownAtkN = -1, m_shownDeckN = -1;
        Button[] m_cardButtons = new Button[0];
        CanvasGroup[] m_cardGroups = new CanvasGroup[0];
        CachedText[] m_cardCd = new CachedText[0];
        int[] m_cardIds = new int[0];

        PvpDuel m_duel;
        Game m_game;
        int m_lastClock = -1, m_lastCount = -1, m_meShown = -1, m_oppShown = -1;
        float m_goTimer;

        /// <summary>상대 미니맵이 들어갈 자리 (VS 줄 오른쪽 끝)</summary>
        public RectTransform OppSlot { get; private set; }
        public bool Visible => m_vs != null && m_vs.gameObject.activeSelf;
        /// <summary>시작 카운트다운 중 (소환·영웅·던지기를 받지 않는다)</summary>
        public bool CountingDown => m_duel != null && m_duel.CountingDown;
        /// <summary>첫 대전: 한 번 던질 때까지 소환 잠금 (ui.ts summonLock)</summary>
        public bool SummonLocked { get; set; }
        /// <summary>몹 카드 i 번 (Game.AttackCards 순서)</summary>
        public RectTransform CardRect(int i) => (RectTransform)m_cardButtons[i].transform;
        public int CardCount => m_cardButtons.Length;
        public int CardId(int i) => m_cardIds[i];

        /// <summary>카드 누름·끌기·뗌 (ThrowInput 이 받는다)</summary>
        public event Action<int, PointerEventData> CardDown, CardDrag, CardUp;

        /// <param name="board">전장 칸 (BattleRunner.BoardArea - 'BoardArea' 또는 그 안의 'Board')</param>
        public PvpHud(RectTransform board, SpriteCatalog cat)
        {
            // HUD 안전 영역 = 'TopBar' 를 자식으로 가진 조상, 전장 칸 = 그 바로 아래 조상
            Transform area = board;
            while (area.parent != null && area.parent.Find("TopBar") == null) area = area.parent;
            m_board = (RectTransform)area;
            m_safe = (RectTransform)area.parent;
            m_top = (RectTransform)m_safe.Find("TopBar");
            m_progress = (RectTransform)m_safe.Find("ProgressRow");
            m_controls = (RectTransform)m_safe.Find("Controls");
            m_cat = cat;
            m_boardBottom = m_board.offsetMin.y;
            m_progressBottom = m_progress.offsetMin.y;
            m_progressTop = m_progress.offsetMax.y;
            m_controlsTop = m_controls.offsetMax.y;
        }

        void Build()
        {
            float topH = -m_top.offsetMin.y; // 상단 바 높이
            m_vs = Ui.Image(m_safe, null, Strip, "PvpVs").rectTransform;
            m_vs.anchorMin = new Vector2(0, 1);
            m_vs.anchorMax = new Vector2(1, 1);
            m_vs.offsetMin = new Vector2(0, -topH - VsHeight);
            m_vs.offsetMax = new Vector2(0, -topH);
            Ui.Row(m_vs, 10, 14, 10, 8, 8, TextAnchor.MiddleLeft);

            RectTransform info = Ui.Rect(m_vs, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 4, 0, 0, 0, 0, TextAnchor.UpperLeft);

            RectTransform names = Ui.Rect(info, "Names");
            Ui.Size(names.gameObject, -1, 32);
            Ui.Row(names, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Tag(names, Loc.Tl("나"), MeFill);
            m_myNick = new CachedText(Flex(Ui.Line(names, "", 24, Ui.Cream, TextAlignmentOptions.Left)));
            m_clock = new CachedText(Fixed(Ui.Line(names, "VS", 26, Ui.Gold, TextAlignmentOptions.Center), 92));
            m_oppNick = new CachedText(Flex(Ui.Line(names, "", 24, Ui.Cream, TextAlignmentOptions.Right)));
            Tag(names, Loc.Tl("상대"), OppFill);

            RectTransform bars = Ui.Rect(info, "Bars");
            Ui.Size(bars.gameObject, -1, 26);
            HorizontalLayoutGroup bl = Ui.Row(bars, 10);
            bl.childForceExpandWidth = true;
            m_meFill = Bar(bars, MeFill, out m_meCount, out _);
            m_oppFill = Bar(bars, OppFill, out m_oppCount, out m_oppFillImage);

            RectTransform deck = Ui.Rect(info, "Deck");
            Ui.Size(deck.gameObject, -1, 40);
            Ui.Row(deck, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Size(Ui.Line(deck, Loc.Tl("상대 카드"), 18, Ui.Cream2, TextAlignmentOptions.Left).gameObject, 96, -1);
            m_oppDeck = Ui.Rect(deck, "Icons");
            Ui.Size(m_oppDeck.gameObject, -1, -1, 1);
            Ui.Row(m_oppDeck, 2, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            m_oppDeckIcons = new Image[OppFrame.MaxDeck * 2];
            for (int i = 0; i < m_oppDeckIcons.Length; i++)
            {
                Image img = Ui.Image(m_oppDeck, null, Color.white, "Card");
                img.preserveAspect = true;
                Ui.Size(img.gameObject, 36, 36);
                img.gameObject.SetActive(false);
                m_oppDeckIcons[i] = img;
            }
            m_oppSub = new CachedText(Fixed(Ui.Line(deck, "", 18, Ui.Cream2, TextAlignmentOptions.Right), 150));

            OppSlot = Ui.Rect(m_vs, "OppSlot");
            Ui.Size(OppSlot.gameObject, VsHeight - 16, VsHeight - 16);

            // 몹 카드 줄 (조작부 바로 위)
            m_cards = Ui.Image(m_safe, null, Strip, "PvpCards").rectTransform;
            m_cards.anchorMin = Vector2.zero;
            m_cards.anchorMax = new Vector2(1, 0);
            m_cards.offsetMin = new Vector2(0, m_controlsTop);
            m_cards.offsetMax = new Vector2(0, m_controlsTop + CardsHeight);
            HorizontalLayoutGroup cl = Ui.Row(m_cards, 10, 14, 14, 8, 8);
            cl.childForceExpandWidth = true;

            // 카운트다운 (전장 가운데)
            m_countRoot = Ui.Rect(m_board, "Countdown");
            Ui.Fill(m_countRoot);
            Ui.Column(m_countRoot, 6, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            m_count = new CachedText(Ui.Line(m_countRoot, "", 140, Ui.Gold));
            m_countSub = Ui.Line(m_countRoot, Loc.Tl("몹을 보내 상대를 무너뜨리세요!"), 30, Ui.Cream);
        }

        static TextMeshProUGUI Flex(TextMeshProUGUI t) { Ui.Size(t.gameObject, -1, -1, 1); return t; }
        static TextMeshProUGUI Fixed(TextMeshProUGUI t, float w) { Ui.Size(t.gameObject, w, -1); return t; }

        static void Tag(Transform parent, string text, Color32 c)
        {
            Image bg = Ui.Image(parent, null, c, "Tag");
            Ui.Size(bg.gameObject, 56, 28);
            Ui.Fill(Ui.Line(bg.transform, text, 18, Ui.Cream).rectTransform, 2, 0, 2, 0);
        }

        static RectTransform Bar(Transform parent, Color32 fill, out CachedText count, out Image fillImage)
        {
            Image bg = Ui.Image(parent, null, BarBg, "Bar");
            Ui.Size(bg.gameObject, -1, 26, 1);
            fillImage = Ui.Image(bg.transform, null, fill, "Fill");
            RectTransform f = fillImage.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(0, 1);
            f.offsetMin = f.offsetMax = Vector2.zero;
            TextMeshProUGUI t = Ui.Line(bg.transform, "0/" + Waves.GameOverMobCount, 18, Ui.Cream);
            Ui.Fill(t.rectTransform);
            count = new CachedText(t);
            return f;
        }

        void BuildCards(Game g)
        {
            Ui.Clear(m_cards);
            IReadOnlyList<int> ids = g.AttackCards;
            m_cardButtons = new Button[ids.Count];
            m_cardGroups = new CanvasGroup[ids.Count];
            m_cardCd = new CachedText[ids.Count];
            m_cardIds = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                int slot = i;
                AttackDef a = Attacks.Get(ids[i]);
                m_cardIds[i] = ids[i];
                Button b = Ui.Button(m_cards, "paper_dark", null, 0, null, 1.8f);
                Ui.Size(b.gameObject, -1, CardsHeight - 16, 1);
                Ui.Column(b.transform, 0, 6, 6, 4, 6, TextAnchor.MiddleCenter);
                Image pic = Ui.Image(b.transform, m_cat.Mob(a.Race, a.Variant)[1], Color.white, "Mob");
                pic.preserveAspect = true;
                Ui.Size(pic.gameObject, 48, 48);
                Ui.Line(b.transform, Loc.Tl(a.Kr), 18, Ui.Cream);
                Ui.Line(b.transform, Loc.Icon("coin") + a.Cost, 18, Ui.Gold);
                TextMeshProUGUI cd = Ui.Line(b.transform, "", 40, Ui.Cream);
                cd.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Ui.Fill(cd.rectTransform);
                m_cardCd[i] = new CachedText(cd);
                m_cardGroups[i] = b.gameObject.AddComponent<CanvasGroup>();
                m_cardButtons[i] = b;
                var relay = b.gameObject.AddComponent<CardInput>();
                relay.Down = e => CardDown?.Invoke(slot, e);
                relay.Drag = e => CardDrag?.Invoke(slot, e);
                relay.Up = e => CardUp?.Invoke(slot, e);
            }
        }

        /// <summary>대전 판 시작: 배치를 바꾸고 상대 정보를 넣는다</summary>
        public void Show(PvpDuel duel, string myNick)
        {
            if (m_vs == null) Build();
            m_duel = duel;
            m_game = duel.Player;
            BuildCards(m_game);
            m_myNick.Set("<noparse>" + myNick + "</noparse>");
            m_oppNick.Set("<noparse>" + duel.Opponent.Nick + "</noparse>");
            m_oppSub.Set(Loc.Icon("trophy") + duel.Opponent.Trophies);
            m_meShown = m_oppShown = m_lastClock = m_lastCount = -1;
            m_shownAtkN = m_shownDeckN = -1;
            foreach (Image i in m_oppDeckIcons) i.gameObject.SetActive(false);
            SetBar(m_oppFill, m_oppCount, 0, ref m_oppShown);
            m_oppFillImage.color = OppFill;
            m_clock.Set("VS");
            duel.Opponent.Snapshot += OnOppSnapshot;
            duel.Go += OnGo;
            m_vs.gameObject.SetActive(true);
            m_cards.gameObject.SetActive(true);
            m_countRoot.gameObject.SetActive(true);
            m_countSub.gameObject.SetActive(true);
            m_goTimer = 0;
            Layout(true);
        }

        public void Hide()
        {
            if (m_duel != null)
            {
                m_duel.Opponent.Snapshot -= OnOppSnapshot;
                m_duel.Go -= OnGo;
            }
            m_duel = null;
            m_game = null;
            if (m_vs == null) return;
            m_vs.gameObject.SetActive(false);
            m_cards.gameObject.SetActive(false);
            m_countRoot.gameObject.SetActive(false);
            Layout(false);
        }

        /// <summary>대전이면 VS 줄·카드 줄만큼 전장을 줄이고, 아니면 원래 배치</summary>
        void Layout(bool pvp)
        {
            float topH = -m_top.offsetMin.y;
            float shift = pvp ? CardsHeight : 0;
            m_progress.offsetMin = new Vector2(m_progress.offsetMin.x, m_progressBottom + shift);
            m_progress.offsetMax = new Vector2(m_progress.offsetMax.x, m_progressTop + shift);
            m_board.offsetMin = new Vector2(m_board.offsetMin.x, m_boardBottom + shift);
            m_board.offsetMax = new Vector2(m_board.offsetMax.x, -topH - (pvp ? VsHeight : 0));
        }

        void OnGo()
        {
            m_count.Set("GO!");
            m_countSub.gameObject.SetActive(false);
            m_goTimer = 0.5f;
        }

        /// <summary>상대 전장 요약 (ui.ts setOpponent): 필드 몹 막대, 상대 카드는 바뀔 때만</summary>
        void OnOppSnapshot(BoardSnapshot s)
        {
            SetBar(m_oppFill, m_oppCount, s.MobCount, ref m_oppShown);
            m_oppFillImage.color = s.MobCount >= Waves.GameOverMobCount * 0.7 ? WarnFill : OppFill;
            if (Same(s.Atk, m_shownAtk, m_shownAtkN) && Same(s.Deck, m_shownDeck, m_shownDeckN)) return;
            m_shownAtkN = Math.Min(OppFrame.MaxDeck, s.Atk.Count);
            m_shownDeckN = Math.Min(OppFrame.MaxDeck, s.Deck.Count);
            int n = 0;
            for (int i = 0; i < m_shownAtkN; i++)
            {
                m_shownAtk[i] = s.Atk[i];
                AttackDef a = Attacks.Get(s.Atk[i]);
                Icon(n++, m_cat.Mob(a.Race, a.Variant)[1]);
            }
            for (int i = 0; i < m_shownDeckN; i++)
            {
                m_shownDeck[i] = s.Deck[i];
                Icon(n++, m_cat.Unit((Job)s.Deck[i], 0)[1]);
            }
            for (int i = n; i < m_oppDeckIcons.Length; i++) m_oppDeckIcons[i].gameObject.SetActive(false);
        }

        void Icon(int i, Sprite s)
        {
            if (i >= m_oppDeckIcons.Length) return;
            m_oppDeckIcons[i].sprite = s;
            m_oppDeckIcons[i].gameObject.SetActive(true);
        }

        static bool Same(List<int> src, int[] shown, int n)
        {
            if (n < 0 || src.Count != n) return false;
            for (int i = 0; i < n; i++) if (src[i] != shown[i]) return false;
            return true;
        }

        void SetBar(RectTransform fill, CachedText label, int mobs, ref int shown)
        {
            if (mobs == shown) return;
            shown = mobs;
            fill.anchorMax = new Vector2(Mathf.Clamp01(mobs / (float)Waves.GameOverMobCount), 1);
            int k = CharText.PutInt(m_buf, 0, mobs);
            k = CharText.Put(m_buf, k, '/');
            label.Set(m_buf, CharText.PutInt(m_buf, k, Waves.GameOverMobCount));
        }

        /// <summary>매 프레임 (BattleHud.Refresh): 내 막대·경과 시간·카운트다운·카드 상태</summary>
        public void Tick(Game g, float unscaledDt)
        {
            if (m_duel == null || g != m_game) return;
            SetBar(m_meFill, m_meCount, g.Mobs.Count, ref m_meShown);

            if (m_duel.CountingDown)
            {
                int c = (int)Math.Ceiling(m_duel.CountdownLeft);
                if (c != m_lastCount) { m_lastCount = c; m_count.Set(m_buf, CharText.PutInt(m_buf, 0, Math.Max(1, c))); }
            }
            else if (m_goTimer > 0)
            {
                m_goTimer -= unscaledDt;
                if (m_goTimer <= 0) m_countRoot.gameObject.SetActive(false);
            }
            else
            {
                int s = (int)m_duel.Elapsed;
                if (s != m_lastClock)
                {
                    m_lastClock = s;
                    int k = CharText.PutInt(m_buf, 0, s / 60);
                    k = CharText.Put(m_buf, k, ':');
                    if (s % 60 < 10) k = CharText.Put(m_buf, k, '0');
                    m_clock.Set(m_buf, CharText.PutInt(m_buf, k, s % 60));
                }
            }

            bool counting = m_duel.CountingDown;
            for (int i = 0; i < m_cardButtons.Length; i++)
            {
                int card = m_cardIds[i];
                double cd = g.AttackCooldown(card);
                bool on = g.Gold >= g.AttackCost(card) && cd <= 0 && !g.Over && !counting;
                if (m_cardButtons[i].interactable != on)
                {
                    m_cardButtons[i].interactable = on;
                    m_cardGroups[i].alpha = on ? 1f : 0.55f;
                }
                // 쿨타임 숫자 (실제 초, ui.ts Math.ceil(cd / PLAY_SPEED))
                int left = cd > 0 ? (int)Math.Ceiling(cd / Waves.PlaySpeed) : 0;
                if (left > 0) m_cardCd[i].Set(m_buf, CharText.PutInt(m_buf, 0, left));
                else m_cardCd[i].Set(m_buf, 0);
            }
        }
    }
}
