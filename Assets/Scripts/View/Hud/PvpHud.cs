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
        public const float VsHeight = 124, CardsHeight = 104;
        // style.css .opp-info / .vs-bar / .vs-tag / #opp-deck 색 그대로
        static readonly Color32 VsBg = new Color32(0x3b, 0x24, 0x16, 255), Edge = new Color32(0x2a, 0x1d, 0x16, 255),
            BarBg = new Color32(0x4a, 0x30, 0x20, 255), Fill = new Color32(0xe5, 0x48, 0x4d, 255), WarnFill = new Color32(0xff, 0x6c, 0x73, 255),
            Shine = new Color32(255, 255, 255, 77), MeTag = new Color32(0x3c, 0x8d, 0x3a, 255), MeTagShade = new Color32(0x24, 0x58, 0x1f, 255),
            OppTag = new Color32(0xb8, 0x32, 0x2c, 255), OppTagShade = new Color32(0x7a, 0x1c, 0x18, 255),
            Tile = new Color32(26, 18, 12, 140), TileSolid = new Color32(0x2c, 0x1d, 0x14, 255), AtkTile = new Color32(0xe5, 0x48, 0x4d, 255), CardOff = new Color32(150, 150, 150, 255);
        /// <summary>몹 카드 4장 테두리 색 (style.css .attack-card.c0~c3)</summary>
        static readonly string[] s_cardFrames = { "btn_red", "btn_blue", "btn_green", "btn_purple" };

        readonly RectTransform m_safe, m_top, m_board, m_progress, m_controls;
        readonly SpriteCatalog m_cat;
        readonly char[] m_buf = new char[64];
        float m_boardBottom, m_boardTop, m_controlsTop;

        RectTransform m_vs, m_cards, m_countRoot;
        RectTransform m_meFill, m_oppFill;
        Image m_meFillImage, m_oppFillImage;
        Image[] m_cardFrames = new Image[0];
        GameObject[] m_cardCdBg = new GameObject[0];
        CachedText m_stageClock;
        CachedText m_meCount, m_oppCount, m_count, m_oppNick, m_myNick, m_oppSub;
        TextMeshProUGUI m_countSub, m_meTag, m_oppTag, m_deckLabel;
        RectTransform m_oppDeck;
        Image[] m_oppDeckIcons;
        Image[] m_oppDeckTiles;
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
        /// <summary>첫 대전 던지기 안내가 떠 있음 (판 안 팁을 겹쳐 띄우지 않는다 - ui.ts maybeTip: hintTimer)</summary>
        public bool HintActive => m_hint != null && m_hint.Active;
        /// <summary>카운트다운 끝 GO! 소리 (ui.ts countdown: audio.sendAttack) - BattleHud 가 넣는다</summary>
        public Action GoSound;
        /// <summary>상단 바 스테이지 글자 - 대전 중엔 경과 시간 (ui.ts #match-timer) - BattleHud 가 넣는다</summary>
        public TMP_Text StageLabel;
        /// <summary>몹 카드 i 번 (Game.AttackCards 순서)</summary>
        public RectTransform CardRect(int i) => (RectTransform)m_cardButtons[i].transform;
        public int CardCount => m_cardButtons.Length;
        public int CardId(int i) => m_cardIds[i];

        /// <summary>카드 누름·끌기·뗌 (ThrowInput 이 받는다)</summary>
        public event Action<int, PointerEventData> CardDown, CardDrag, CardUp;

        readonly BattleRunner m_runner;
        OppBoardView m_oppView;
        OppPeek m_peek;
        ThrowInput m_throw;
        ThrowHint m_hint;

        /// <summary>안내 글 (던지는 법) / 골드 배지 깜빡임 - BattleHud 가 넣는다</summary>
        public Toast Toast;
        public Action FlashGold;

        /// <summary>상대 전장 그림 / 미니맵 (처음 대전할 때 만든다)</summary>
        public OppBoardView OppView => m_oppView;
        public OppPeek Peek => m_peek;

        public PvpHud(BattleRunner runner)
        {
            m_runner = runner;
            SpriteCatalog cat = runner.Catalog;
            // HUD 안전 영역 = 'TopBar' 를 자식으로 가진 조상, 전장 칸 = 그 바로 아래 조상 (BoardArea 또는 그 안의 Board 에서 위로)
            Transform area = runner.BoardArea;
            while (area.parent != null && area.parent.Find("TopBar") == null) area = area.parent;
            m_board = (RectTransform)area;
            m_safe = (RectTransform)area.parent;
            m_top = (RectTransform)m_safe.Find("TopBar");
            m_progress = (RectTransform)m_safe.Find("ProgressRow");
            m_controls = (RectTransform)m_safe.Find("Controls");
            m_cat = cat;
            m_boardBottom = m_board.offsetMin.y;
            m_controlsTop = m_controls.offsetMax.y;
            m_boardTop = m_board.offsetMax.y;
        }

        void Build()
        {
            float topH = -m_top.offsetMin.y; // 상단 바 높이
            m_vs = Ui.Image(m_safe, null, VsBg, "PvpVs").rectTransform;
            m_vs.anchorMin = new Vector2(0, 1);
            m_vs.anchorMax = new Vector2(1, 1);
            m_vs.offsetMin = new Vector2(0, -topH - VsHeight);
            m_vs.offsetMax = new Vector2(0, -topH);
            Ui.Row(m_vs, 12, 14, 12, 8, 10, TextAnchor.MiddleLeft);
            Image edge = Ui.Image(m_vs, null, Edge, "Edge"); // 아래 테두리 (border-bottom: 2px edge)
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = new Vector2(1, 0);
            edge.rectTransform.offsetMin = Vector2.zero;
            edge.rectTransform.offsetMax = new Vector2(0, 4);

            RectTransform info = Ui.Rect(m_vs, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 4, 0, 0, 0, 0, TextAnchor.UpperLeft);

            RectTransform names = Ui.Rect(info, "Names");
            Ui.Size(names.gameObject, -1, 32);
            Ui.Row(names, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            m_meTag = Tag(names, MeTag, MeTagShade);
            m_myNick = new CachedText(Flex(Ui.Line(names, "", 24, Ui.Cream, TextAlignmentOptions.Left)));
            Fixed(Ui.Line(names, "VS", 26, Ui.Gold, TextAlignmentOptions.Center), 56);
            m_oppNick = new CachedText(Flex(Ui.Line(names, "", 24, Ui.Cream, TextAlignmentOptions.Right)));
            m_oppTag = Tag(names, OppTag, OppTagShade);

            RectTransform bars = Ui.Rect(info, "Bars");
            Ui.Size(bars.gameObject, -1, 26);
            HorizontalLayoutGroup bl = Ui.Row(bars, 10);
            bl.childForceExpandWidth = true;
            m_meFill = Bar(bars, false, out m_meCount, out m_meFillImage);
            m_oppFill = Bar(bars, true, out m_oppCount, out m_oppFillImage); // 상대 막대는 오른쪽에서 찬다

            RectTransform deck = Ui.Rect(info, "Deck");
            Ui.Size(deck.gameObject, -1, 40);
            Ui.Row(deck, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            m_deckLabel = Fixed(Ui.Line(deck, "", 18, Ui.Cream2, TextAlignmentOptions.Left), 96);
            m_oppDeck = Ui.Rect(deck, "Icons");
            Ui.Size(m_oppDeck.gameObject, -1, -1, 1);
            Ui.Row(m_oppDeck, 2, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            m_oppDeckIcons = new Image[OppFrame.MaxDeck * 2];
            m_oppDeckTiles = new Image[m_oppDeckIcons.Length];
            for (int i = 0; i < m_oppDeckIcons.Length; i++)
            {
                // 칸 (어두운 바탕, 몹 카드는 빨간 테두리) + 그림 (#opp-deck .frame-sprite)
                Image tile = Ui.Image(m_oppDeck, null, TileSolid, "Card");
                Ui.Size(tile.gameObject, 34, 34);
                Image inner = Ui.Image(tile.transform, null, TileSolid, "In"); // 불투명 - 빨간 테두리가 안쪽에 비치지 않게
                Ui.Fill(inner.rectTransform, 2, 2, 2, 2);
                Image img = Ui.Image(inner.transform, null, Color.white, "Pic");
                img.preserveAspect = true;
                Ui.Fill(img.rectTransform, 1, 1, 1, 1);
                tile.gameObject.SetActive(false);
                m_oppDeckTiles[i] = tile;
                m_oppDeckIcons[i] = img;
            }
            m_oppSub = new CachedText(Fixed(Ui.Line(deck, "", 18, Ui.Cream2, TextAlignmentOptions.Right), 150));

            OppSlot = Ui.Rect(m_vs, "OppSlot");
            Ui.Size(OppSlot.gameObject, VsHeight - 16, VsHeight - 16);

            // 몹 카드 줄: 나무 조작부 안 맨 위 (index.html #controls > #attack-row) - 조작부를 그만큼 높인다
            m_cards = Ui.Rect(m_controls.Find("Content"), "PvpCards");
            m_cards.SetSiblingIndex(0);
            Ui.Size(m_cards.gameObject, -1, CardsHeight - 12);
            HorizontalLayoutGroup cl = Ui.Row(m_cards, 12);
            cl.childForceExpandWidth = true;
            cl.childForceExpandHeight = true;

            m_throw = new ThrowInput(this, m_safe, m_cat) { Toast = Toast, FlashGold = FlashGold };
            m_throw.Thrown += OnThrown;
            m_hint = new ThrowHint(m_safe);

            // 카운트다운 (전장 가운데)
            m_countRoot = Ui.Rect(m_board, "Countdown");
            Ui.Fill(m_countRoot);
            Ui.Column(m_countRoot, 6, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            m_count = new CachedText(Ui.Line(m_countRoot, "", 140, Ui.Gold));
            m_countSub = Ui.Line(m_countRoot, "", 30, Ui.Cream);
        }

        static TextMeshProUGUI Flex(TextMeshProUGUI t) { Ui.Size(t.gameObject, -1, -1, 1); return t; }
        static TextMeshProUGUI Fixed(TextMeshProUGUI t, float w) { Ui.Size(t.gameObject, w, -1); return t; }

        /// <summary>나·상대 표 (.vs-tag: 색 바탕 + 아래 진한 줄)</summary>
        static TextMeshProUGUI Tag(Transform parent, Color32 c, Color32 shade)
        {
            Image bg = Ui.Image(parent, null, c, "Tag");
            Ui.Size(bg.gameObject, 58, 30);
            Image sh = Ui.Image(bg.transform, null, shade, "Shade");
            sh.rectTransform.anchorMin = Vector2.zero;
            sh.rectTransform.anchorMax = new Vector2(1, 0);
            sh.rectTransform.offsetMin = Vector2.zero;
            sh.rectTransform.offsetMax = new Vector2(0, 4);
            TextMeshProUGUI t = Ui.Line(bg.transform, "", 18, Ui.Cream);
            Ui.Fill(t.rectTransform, 2, 3, 2, 0);
            return t;
        }

        /// <summary>필드 몹 막대 (.vs-bar: 테두리 + 갈색 바탕 + 빨간 채움 위 밝은 줄). fromRight = 오른쪽에서 찬다</summary>
        static RectTransform Bar(Transform parent, bool fromRight, out CachedText count, out Image fillImage)
        {
            Image frame = Ui.Image(parent, null, Edge, "Bar");
            Ui.Size(frame.gameObject, -1, 30, 1);
            Image bg = Ui.Image(frame.transform, null, BarBg, "Bg");
            Ui.Fill(bg.rectTransform, 3, 3, 3, 3);
            fillImage = Ui.Image(bg.transform, null, Fill, "Fill");
            RectTransform f = fillImage.rectTransform;
            f.anchorMin = new Vector2(fromRight ? 1 : 0, 0);
            f.anchorMax = new Vector2(fromRight ? 1 : 0, 1);
            f.offsetMin = f.offsetMax = Vector2.zero;
            Image shine = Ui.Image(f, null, Shine, "Shine");
            shine.rectTransform.anchorMin = new Vector2(0, 1);
            shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.offsetMin = new Vector2(0, -4);
            shine.rectTransform.offsetMax = Vector2.zero;
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
            m_cardFrames = new Image[ids.Count];
            m_cardCdBg = new GameObject[ids.Count];
            m_cardGroups = new CanvasGroup[ids.Count];
            m_cardCd = new CachedText[ids.Count];
            m_cardIds = new int[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                int slot = i;
                AttackDef a = Attacks.Get(ids[i]);
                m_cardIds[i] = ids[i];
                string frame = s_cardFrames[i % s_cardFrames.Length];
                Button b = Ui.Button(m_cards, frame, null, 0, null, 1.7f);
                b.spriteState = new SpriteState { pressedSprite = m_cat.Frame(frame + "_down"), disabledSprite = m_cat.Frame(frame) };
                Ui.Size(b.gameObject, -1, -1, 1);
                Ui.Column(b.transform, 0, 10, 10, 8, 14, TextAnchor.MiddleCenter);
                Image pic = Ui.Image(b.transform, m_cat.Mob(a.Race, a.Variant)[1], Color.white, "Mob");
                pic.preserveAspect = true;
                Ui.Size(pic.gameObject, 46, 46);
                Ui.Line(b.transform, Loc.Tl(a.Kr), 18, Ui.Cream);
                Ui.Line(b.transform, Loc.Icon("coin") + a.Cost, 18, Ui.Cream);
                // 쿨타임: 어두운 덮개 + 남은 초 (.attack-card .cd)
                Image cdBg = Ui.Image(b.transform, null, Tile, "Cd");
                cdBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Ui.Fill(cdBg.rectTransform, 6, 12, 6, 6);
                TextMeshProUGUI cd = Ui.Line(cdBg.transform, "", 34, Ui.Cream);
                Ui.Fill(cd.rectTransform);
                cdBg.gameObject.SetActive(false);
                m_cardFrames[i] = b.image;
                m_cardCdBg[i] = cdBg.gameObject;
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
            // 고정 글자는 대전마다 지금 언어로 (설정에서 언어를 바꿀 수 있다)
            m_meTag.text = Loc.Tl("나");
            m_oppTag.text = Loc.Tl("상대");
            m_deckLabel.text = Loc.Tl("상대 카드");
            m_countSub.text = Loc.Tl("몹을 보내 상대를 무너뜨리세요!");
            m_myNick.Set("<noparse>" + myNick + "</noparse>");
            m_oppNick.Set("<noparse>" + duel.Opponent.Nick + "</noparse>");
            m_oppSub.Set(Loc.Icon("trophy") + duel.Opponent.Trophies);
            m_meShown = m_oppShown = m_lastClock = m_lastCount = -1;
            m_shownAtkN = m_shownDeckN = -1;
            foreach (Image t in m_oppDeckTiles) t.gameObject.SetActive(false);
            SetBar(m_oppFill, m_oppCount, 0, ref m_oppShown);
            m_oppFillImage.color = Fill;
            m_meFillImage.color = Fill;
            if (m_stageClock == null && StageLabel != null) m_stageClock = new CachedText(StageLabel);
            if (m_stageClock != null) m_stageClock.Set("PvP");
            duel.Opponent.Snapshot += OnOppSnapshot;
            duel.Go += OnGo;
            if (m_oppView == null)
            {
                m_oppView = OppBoardView.Create(m_cat, m_runner.SpriteMaterial, m_runner.ShapeMaterial);
                m_peek = OppPeek.Create(m_safe, OppSlot, m_board, m_oppView.Texture);
                // HUD 위, 던지는 몹·안내 아래
                m_peek.transform.SetSiblingIndex(m_vs.GetSiblingIndex() + 1);
            }
            m_oppView.Begin(duel.Opponent);
            m_peek.gameObject.SetActive(true);
            m_throw.Bind(m_game);
            // 첫 대전: 던지는 법 안내 + 한 번 던질 때까지 소환 잠금 (main.ts: wins + losses === 0)
            SummonLocked = PvpRules.IsFirstMatch(EconCache.State);
            if (SummonLocked) m_hint.Show(); else m_hint.Hide();
            m_vs.gameObject.SetActive(true);
            m_cards.gameObject.SetActive(true);
            m_countRoot.gameObject.SetActive(true);
            m_countSub.gameObject.SetActive(true);
            m_count.Set(m_buf, CharText.PutInt(m_buf, 0, (int)PvpDuel.CountdownSec)); // 지난 판의 GO! 가 한 프레임 보이지 않게
            m_lastCount = (int)PvpDuel.CountdownSec;
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
            if (m_oppView != null) m_oppView.End();
            if (m_throw != null) { m_throw.Bind(null); m_hint.Hide(); }
            SummonLocked = false;
            if (m_peek != null) { m_peek.Aiming = false; m_peek.gameObject.SetActive(false); }
            if (m_vs == null) return;
            m_vs.gameObject.SetActive(false);
            m_cards.gameObject.SetActive(false);
            m_countRoot.gameObject.SetActive(false);
            Layout(false);
        }

        /// <summary>대전이면 VS 줄·카드 줄만큼 전장을 줄이고, 아니면 원래 배치</summary>
        void Layout(bool pvp)
        {
            // 대전에선 아래 진행 막대를 숨기고(위 VS 막대가 내 몹 수를 보여 준다) 그 자리까지 전장으로 (style.css #game-frame.pvp #progress-row)
            m_progress.gameObject.SetActive(!pvp);
            m_controls.offsetMax = new Vector2(m_controls.offsetMax.x, m_controlsTop + (pvp ? CardsHeight : 0));
            m_board.offsetMin = new Vector2(m_board.offsetMin.x, pvp ? m_controlsTop + CardsHeight : m_boardBottom);
            m_board.offsetMax = new Vector2(m_board.offsetMax.x, m_boardTop - (pvp ? VsHeight : 0)); // 대전이 아니면 처음 값 그대로
            // 전장이 정사각형이라 남는 양옆은 어두운 갈색 (style.css #game-frame.pvp #battlefield background #3b2416)
            Camera cam = m_runner.Camera;
            if (cam != null)
            {
                if (pvp && !m_camSaved) { m_camColor = cam.backgroundColor; m_camSaved = true; }
                if (pvp) cam.backgroundColor = PvpBackdrop;
                else if (m_camSaved) cam.backgroundColor = m_camColor;
            }
        }

        static readonly Color32 PvpBackdrop = new Color32(0x3b, 0x24, 0x16, 255);
        Color m_camColor;
        bool m_camSaved;

        void OnThrown()
        {
            m_hint.Hide();
            SummonLocked = false;
        }

        /// <summary>몹 카드 slot 을 지금 던질 수 있나</summary>
        public bool CardEnabled(int slot) => slot >= 0 && slot < m_cardButtons.Length && m_cardButtons[slot].interactable;

        void OnGo()
        {
            m_count.Set("GO!");
            GoSound?.Invoke();
            m_countSub.gameObject.SetActive(false);
            m_goTimer = 0.5f;
        }

        /// <summary>상대 전장 요약 (ui.ts setOpponent): 필드 몹 막대, 상대 카드는 바뀔 때만</summary>
        void OnOppSnapshot(BoardSnapshot s)
        {
            SetBar(m_oppFill, m_oppCount, s.MobCount, ref m_oppShown);
            m_oppFillImage.color = s.MobCount >= Waves.GameOverMobCount * 0.7 ? WarnFill : Fill;
            if (Same(s.Atk, m_shownAtk, m_shownAtkN) && Same(s.Deck, m_shownDeck, m_shownDeckN)) return;
            m_shownAtkN = Math.Min(OppFrame.MaxDeck, s.Atk.Count);
            m_shownDeckN = Math.Min(OppFrame.MaxDeck, s.Deck.Count);
            int n = 0;
            for (int i = 0; i < m_shownAtkN; i++)
            {
                m_shownAtk[i] = s.Atk[i];
                AttackDef a = Attacks.Get(s.Atk[i]);
                Icon(n++, m_cat.Mob(a.Race, a.Variant)[1], true);
            }
            for (int i = 0; i < m_shownDeckN; i++)
            {
                m_shownDeck[i] = s.Deck[i];
                Icon(n++, m_cat.Unit((Job)s.Deck[i], 0)[1], false);
            }
            for (int i = n; i < m_oppDeckTiles.Length; i++) m_oppDeckTiles[i].gameObject.SetActive(false);
        }

        /// <summary>상대 카드 칸 i 번 (몹 카드는 빨간 테두리)</summary>
        void Icon(int i, Sprite s, bool attack)
        {
            if (i >= m_oppDeckIcons.Length) return;
            m_oppDeckIcons[i].sprite = s;
            m_oppDeckTiles[i].color = attack ? AtkTile : TileSolid;
            m_oppDeckTiles[i].gameObject.SetActive(true);
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
            float pct = Mathf.Clamp01(mobs / (float)Waves.GameOverMobCount);
            // 내 막대는 왼쪽에서, 상대 막대는 오른쪽에서 찬다 (style.css .vs-bar.me / .opp)
            if (fill.anchorMin.x > 0 || fill == m_oppFill) fill.anchorMin = new Vector2(1 - pct, 0);
            else fill.anchorMax = new Vector2(pct, 1);
            int k = CharText.PutInt(m_buf, 0, mobs);
            k = CharText.Put(m_buf, k, '/');
            label.Set(m_buf, CharText.PutInt(m_buf, k, Waves.GameOverMobCount));
        }

        /// <summary>매 프레임 (BattleHud.Refresh): 내 막대·경과 시간·카운트다운·카드 상태</summary>
        public void Tick(Game g, float unscaledDt)
        {
            if (m_duel == null || g != m_game) return;
            int before = m_meShown;
            SetBar(m_meFill, m_meCount, g.Mobs.Count, ref m_meShown);
            if (before != m_meShown) m_meFillImage.color = g.Mobs.Count >= Waves.GameOverMobCount * 0.7 ? WarnFill : Fill;

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
                    int k = CharText.Put(m_buf, 0, "<sprite name=\"timer\">");
                    k = CharText.PutInt(m_buf, k, s / 60);
                    k = CharText.Put(m_buf, k, ':');
                    if (s % 60 < 10) k = CharText.Put(m_buf, k, '0');
                    if (m_stageClock != null) m_stageClock.Set(m_buf, CharText.PutInt(m_buf, k, s % 60));
                }
            }

            bool counting = m_duel.CountingDown;
            m_throw.Tick(unscaledDt);
            if (m_hint.Active)
            {
                RectTransform card = null;
                // 버튼 활성 상태(아래 반복문이 이번 프레임에 고친다)가 아니라 게임 상태로 - 카운트다운이 끝난 첫 프레임에 잠금이 풀리지 않게
                for (int i = 0; i < m_cardIds.Length && card == null; i++) if (PvpRules.CanThrow(g, m_cardIds[i])) card = CardRect(i);
                if (card == null && !counting) SummonLocked = false; // 던질 카드가 없으면 막아 둘 이유가 없다
                m_hint.Tick(unscaledDt, card, m_peek.View, counting || g.Over);
            }
            for (int i = 0; i < m_cardButtons.Length; i++)
            {
                int card = m_cardIds[i];
                double cd = g.AttackCooldown(card);
                bool on = PvpRules.CanThrow(g, card) && !counting;
                if (m_cardButtons[i].interactable != on)
                {
                    m_cardButtons[i].interactable = on;
                    m_cardGroups[i].alpha = on ? 1f : 0.6f; // .attack-card:disabled - 흐리고 회색으로
                    m_cardFrames[i].color = on ? Color.white : (Color)CardOff;
                }
                // 쿨타임 숫자 (실제 초, ui.ts Math.ceil(cd / PLAY_SPEED))
                int left = cd > 0 ? (int)Math.Ceiling(cd / Waves.PlaySpeed) : 0;
                bool cooling = left > 0;
                if (m_cardCdBg[i].activeSelf != cooling) m_cardCdBg[i].SetActive(cooling);
                if (cooling) m_cardCd[i].Set(m_buf, CharText.PutInt(m_buf, 0, left));
            }
        }
    }
}
