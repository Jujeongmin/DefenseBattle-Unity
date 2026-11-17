using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 로비 (menu.ts render): 상단(닉·티어·보석·설정) / 제목·덱 행렬 / 아레나 카드(로드 바·대전·시즌 패스·상자 4칸) /
    /// 모드 카드(캠페인·무한) / 하단 메뉴. 경제 상태가 바뀌거나 페이지에서 돌아오면 다시 그린다 (매 프레임 그리지 않음).
    /// 상자 남은 시간만 1초마다 글자를 바꾼다.
    /// </summary>
    public sealed class Lobby : MonoBehaviour
    {
        [SerializeField] SpriteCatalog m_catalog;
        [SerializeField] GameObject m_root;
        /// <summary>안전 영역 안 내용 칸</summary>
        [SerializeField] RectTransform m_content;
        /// <summary>확인 창·페이지를 띄울 맨 위 칸</summary>
        [SerializeField] RectTransform m_popups;
        [SerializeField] Toast m_toast;
        [SerializeField] BattleAudio m_audio;

        /// <summary>판 시작 (캠페인 스테이지 / 무한)</summary>
        public event Action<BattleLaunch> Play;

        Modal m_modal;
        Modal m_panel;
        CampaignPages m_campaign;
        DeckPage m_deck;
        RevealPopup m_reveal;
        ResearchPage m_research;
        ShopPage m_shop;
        QuestsPage m_quests;
        RoadPage m_road;
        PassPage m_pass;
        RankPage m_rank;
        ProfilePage m_profile;
        SettingsPage m_settings;
        ProductBuy m_buy;
        MatchPopup m_match;
        bool m_busy;
        bool m_roadBusy;
        bool m_dirty;
        float m_tick;
        readonly TextMeshProUGUI[] m_chestClock = new TextMeshProUGUI[4];
        readonly ChestStatus[] m_chestShown = new ChestStatus[4];

        public bool Visible => m_root.activeSelf;
        public Modal Modal => m_modal;
        /// <summary>결제 팝업 (판 끝 특가·보석 부족)</summary>
        public OfferPopup Offers { get; private set; }

        void Awake()
        {
            Ui.Init(m_catalog);
            m_panel = new Modal(m_popups, false);
            m_modal = new Modal(m_popups);
            m_campaign = new CampaignPages(m_catalog, m_panel, m_modal, m_toast, m_audio, Launch, () => m_dirty = true);
            m_deck = new DeckPage(m_catalog, m_panel, m_modal, m_toast, m_audio, () => m_dirty = true);
            m_reveal = new RevealPopup(this, m_popups, m_catalog, m_audio);
            m_buy = new ProductBuy(m_toast, m_audio, m_reveal);
            m_match = new MatchPopup(m_modal, this, m_audio);
            // 결제 팝업은 로비와 전투 결과 화면 위 모두에 뜬다 → 안내 글(Toast) 바로 아래 칸
            RectTransform offerHost = Ui.Rect(m_toast.transform.parent, "OfferHost");
            Ui.Fill(offerHost);
            offerHost.SetSiblingIndex(m_toast.transform.GetSiblingIndex());
            Offers = new OfferPopup(offerHost, m_catalog, m_buy, this);
            m_research = new ResearchPage(m_panel, m_toast, m_audio, () => m_dirty = true);
            m_research.Shortage = need => Offers.Shortage(need);
            m_shop = new ShopPage(m_panel, m_modal, m_toast, m_audio, m_reveal, m_buy, Offers, () => m_dirty = true);
            m_quests = new QuestsPage(m_panel, m_toast, m_audio, () => m_dirty = true);
            m_road = new RoadPage(m_catalog, m_panel, () => m_dirty = true);
            m_pass = new PassPage(m_panel, m_toast, m_audio, () => m_dirty = true);
            m_rank = new RankPage(m_panel, () => m_dirty = true);
            m_profile = new ProfilePage(m_panel, m_toast, () => m_dirty = true);
            m_settings = new SettingsPage(m_panel, m_audio, () => m_dirty = true);
            m_settings.OpenProfile = m_profile.Open;
            m_settings.ReplayTutorial = ReplayTutorial;
            m_campaign.OpenDeck = m_deck.Open; // 스테이지 정보 → 덱 편집 → 다시 스테이지 정보
            m_deck.Shortage = (need, job) => Offers.Shortage(need, job);
            m_pass.BuyPremium = () => m_buy.Buy("season_premium", ok => { if (ok) m_pass.Open(); });
            EconCache.Changed += OnEcon;
            Localization.Changed += OnEcon;
        }

        void OnDestroy()
        {
            EconCache.Changed -= OnEcon;
            Localization.Changed -= OnEcon;
        }

        void OnEcon() => m_dirty = true;

        public void Show()
        {
            m_root.SetActive(true);
            Render();
            if (Progress.FeatureUnlocked(EconCache.State, "pvp")) m_rank.Prefetch(); // 랭킹 창을 처음 열 때도 바로 보이게
        }

        /// <summary>설정 → 튜토리얼 다시 하기: 안내만 처음부터 (스테이지·보상 진행은 그대로), 1-1 정보 창을 연다</summary>
        void ReplayTutorial()
        {
            LocalMetaData m = LocalMeta.Data;
            m.Tutorial = 0;
            m.SeenFeatures.Clear();
            LocalMeta.Save();
            m_panel.Close();
            m_campaign.OpenStageInfo("1-1");
            m_dirty = true;
        }

        public void Hide()
        {
            if (m_guideCo != null) StopCoroutine(m_guideCo);
            m_guideCo = null;
            CloseGuide();
            m_guideRetry = 0;
            m_match.Stop();
            m_modal.Close();
            m_panel.Close();
            m_reveal.Close();
            m_root.SetActive(false);
        }

        // ======================= 로비 안내 (menu.ts maybeGuide) =======================
        // 튜토리얼 뒤 새로 열린 기능 버튼을 하나씩 밝혀 준다 (대전 → 덱 → 캠페인 → 연구 → 퀘스트 → 무한). 본 것은 SeenFeatures
        static readonly Dictionary<string, string> s_guideText = new Dictionary<string, string>
        {
            { "pvp", "<b>대전 시작</b>을 눌러 보세요!<br>상대 전장에 몹을 던지고, 이기면 상자를 받아요" },
            { "units", "<b>덱</b>에서 유닛과 몹 카드를 고르고<br>모은 카드로 강화해요" },
            { "campaign", "<b>캠페인</b>에서 스테이지를 깨면<br>새 유닛과 보석을 얻어요" },
            { "research", "<b>연구소</b>가 열렸어요!<br>보석으로 대전과 모든 판의 능력을 올려요" },
            { "quests", "<b>퀘스트</b>가 열렸어요!<br>매일 목표를 채우면 보석을 받아요" },
            { "endless", "<b>무한 모드</b>가 열렸어요!<br>얼마나 오래 버티는지 도전해 보세요" },
        };
        readonly Dictionary<string, GameObject> m_guideTargets = new Dictionary<string, GameObject>();
        readonly WaitForSecondsRealtime m_guideDelay = new WaitForSecondsRealtime(0.6f);
        Spotlight m_spot;
        bool m_guiding;
        float m_guideRetry;

        GameObject GuideTarget(string key) => m_guideTargets.TryGetValue(key, out GameObject go) && go != null ? go : null;

        /// <summary>팝업·페이지가 떠 있으면 안내하지 않는다</summary>
        bool Covered => m_panel.IsOpen || m_modal.IsOpen || Offers.IsOpen || m_reveal.Busy;

        void MaybeGuide()
        {
            if (m_guiding || !Visible) return;
            string key = GuideRules.NextLobbyGuide(EconCache.State, LocalMeta.Data.Tutorial, LocalMeta.Data.SeenFeatures, k => GuideTarget(k) != null);
            if (key == null) return;
            if (Covered) { m_guideRetry = 0.8f; return; } // 닫힐 때까지 기다렸다가
            m_guiding = true;
            m_guideCo = StartCoroutine(Guide(key));
        }

        Coroutine m_guideCo;

        IEnumerator Guide(string key)
        {
            yield return m_guideDelay;
            // 기다리는 사이 다른 창을 열었거나 판에 들어갔으면 다음 기회로
            if (!Visible || Covered || GuideTarget(key) == null)
            {
                m_guiding = false;
                m_guideCo = null;
                m_guideRetry = 0.8f;
                yield break;
            }
            m_spot = Spotlight.Create(m_popups.GetComponentInParent<CanvasScaler>());
            bool done = false;
            m_spot.Step(() => GuideTarget(key), Loc.Tl(s_guideText[key]), () => done = true);
            while (!done && Visible) yield return null;
            CloseGuide();
            m_guideCo = null;
            if (done) LocalMeta.MarkSeen(key);
        }

        void CloseGuide()
        {
            if (m_spot != null) m_spot.Close();
            m_spot = null;
            m_guiding = false;
        }

        void Update()
        {
            if (!m_root.activeSelf) return;
            if (m_guideRetry > 0)
            {
                m_guideRetry -= Time.unscaledDeltaTime;
                if (m_guideRetry <= 0) MaybeGuide();
            }
            if (m_dirty) Render();
            m_tick -= Time.unscaledDeltaTime;
            if (m_tick > 0) return;
            m_tick = 1;
            long now = NowMs;
            EconState e = EconCache.State;
            for (int i = 0; i < m_chestClock.Length; i++)
            {
                if (m_chestShown[i] != ChestStatus.Opening) continue;
                if (LobbyRules.Status(e, i, now) != ChestStatus.Opening) { Render(); return; }
                if (m_chestClock[i] != null) m_chestClock[i].text = LobbyRules.ClockText(LobbyRules.LeftMs(e, i, now));
            }
        }

        public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ======================= 그리기 =======================
        void Render()
        {
            m_dirty = false;
            EconState e = EconCache.State;
            ClaimRoadIfNeeded(e);
            Ui.Clear(m_content);
            for (int i = 0; i < m_chestClock.Length; i++) { m_chestClock[i] = null; m_chestShown[i] = ChestStatus.Empty; }

            RectTransform col = Ui.Rect(m_content, "Lobby");
            Ui.Fill(col);
            Ui.Column(col, 0);
            TopBar(col, e);
            // 작은 화면에서는 가운데 칸만 스크롤 (menu.ts fitLobby 마지막 단계와 같음)
            RectTransform body = Ui.Scroll(col, out ScrollRect scroll);
            Ui.Size(body.parent.gameObject, -1, -1, -1, 1);
            Ui.Column(body, 12, 28, 28, 6, 16, TextAnchor.UpperCenter);
            m_guideTargets.Clear();
            Body(body, e);
            Nav(col, e);
            MaybeGuide();
        }

        void TopBar(Transform parent, EconState e)
        {
            RectTransform bar = Ui.Rect(parent, "Top");
            Ui.Size(bar.gameObject, -1, 104);
            Ui.Row(bar, 12, 16, 16, 12, 8);
            PvpTier tier = Arenas.Of(e.Pvp.Trophies);
            Color32 tierColor = Ui.Hex(tier.Color);

            Button profile = Ui.Button(bar, "paper_dark", null, 0, () => m_profile.Open(), 1.6f);
            Ui.Size(profile.gameObject, -1, 84, 1);
            Ui.Row(profile.transform, 10, 14, 16, 8, 8, TextAnchor.MiddleLeft);
            Ui.Icon(profile.transform, "trophy", 52, tierColor);
            RectTransform who = Ui.Rect(profile.transform, "Who");
            Ui.Size(who.gameObject, -1, -1, 1);
            Ui.Column(who, 0, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(who, Profile.Nick, 26, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(who, Loc.Tl(tier.Kr) + " · " + e.Pvp.Trophies, 22, tierColor, TextAlignmentOptions.Left);

            Button gems = Ui.Button(bar, "paper_dark", null, 0, OpenShop, 1.6f);
            Ui.Size(gems.gameObject, 210, 72);
            Ui.Row(gems.transform, 6, 12, 12, 6, 6);
            Ui.Icon(gems.transform, "gem", 36);
            TextMeshProUGUI n = Ui.Line(gems.transform, Ui.Num(e.Crystals), 28, Ui.Gem);
            Ui.Size(n.gameObject, -1, -1, 1);
            Ui.Icon(gems.transform, "plus", 28, Ui.Green);

            Button gear = Ui.Button(bar, "btn_blue", null, 0, () => m_settings.Open());
            Ui.Size(gear.gameObject, 84, 84);
            Ui.Fill(Ui.Icon(gear.transform, "settings", 44).rectTransform, 18, 22, 18, 14);
        }

        void Body(Transform body, EconState e)
        {
            Image logo = Ui.Image(body, m_catalog.Frame("logo"), Color.white, "Logo");
            logo.preserveAspect = true;
            Ui.Size(logo.gameObject, -1, 150);

            RectTransform parade = Ui.Rect(body, "Parade");
            Ui.Size(parade.gameObject, -1, 88);
            Ui.Row(parade, 10);
            foreach (Job j in e.Deck)
            {
                Image u = Ui.Image(parade, m_catalog.Unit(j, 0)[1], Color.white, j.ToString());
                u.preserveAspect = true;
                Ui.Size(u.gameObject, 88, 88);
            }

            if (MetaRules.MonthlyClaimable(e, NowMs, LocalMeta.Today)) MonthlyNotice(body);
            if (Progress.FeatureUnlocked(e, "pvp"))
            {
                ArenaCard(body, e);
                ModeRow(body, e);
            }
            else
            {
                Button tut = Ui.Button(body, "btn_red", Loc.Icon("play") + " " + Loc.Tl("튜토리얼 시작"), 40,
                    () => Launch(new BattleLaunch(BattleLaunch.Mode.Stage, "1-1")));
                Ui.Size(tut.gameObject, -1, 120);
            }
        }

        void ArenaCard(Transform body, EconState e)
        {
            PvpTier tier = Arenas.Of(e.Pvp.Trophies);
            Image card = Ui.Frame(body, "paper", 1.4f, "Arena");
            Ui.Column(card.transform, 8, 30, 30, 22, 28);

            Ui.Line(card.transform, Loc.Icon("trophy") + " " + Loc.Tl(tier.Kr) + " " + Loc.Tl("아레나") + "  " + e.Pvp.Trophies, 36, Ui.Hex(tier.Color));

            // 로드 바
            Image track = Ui.Image(card.transform, null, new Color32(0x3b, 0x24, 0x16, 90), "RoadBar");
            Ui.Size(track.gameObject, -1, 22);
            Image fill = Ui.Image(track.transform, null, Ui.Gold, "Fill");
            RectTransform fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2((float)(LobbyRules.RoadPercent(e) / 100), 1);
            fr.offsetMin = fr.offsetMax = Vector2.zero;

            RoadStep next = Progress.NextRoadStep(e);
            if (next != null)
                Ui.Line(card.transform, Loc.Tl("다음 보상") + " " + RoadText(next) + "  " + Loc.Icon("trophy") + next.At, 24, Ui.Ink2, TextAlignmentOptions.Center, true);

            Button battle = Ui.Button(card.transform, "btn_red", Loc.Icon("sword") + " " + Loc.Tl("대전 시작"), 40,
                () => { LocalMeta.MarkSeen("pvp"); QuickMatch(); });
            Ui.Size(battle.gameObject, -1, 100);
            m_guideTargets["pvp"] = battle.gameObject;

            RectTransform sub = Ui.Rect(card.transform, "Sub");
            Ui.Size(sub.gameObject, -1, 64);
            Ui.Row(sub, 16);
            string wl = Loc.Tl("{0}승 {1}패", e.Pvp.Wins, e.Pvp.Losses) + (e.Pvp.Streak > 1 ? Loc.Tl(" · {0}연승", e.Pvp.Streak) : "");
            TextMeshProUGUI wlt = Ui.Line(sub, wl, 24, Ui.Ink, TextAlignmentOptions.Left, true);
            Ui.Size(wlt.gameObject, -1, -1, 1);
            Button pass = Ui.Button(sub, "btn_gold", Loc.Icon("trophy") + " " + Loc.Tl("시즌 패스"), 24, () => m_pass.Open());
            if (MetaRules.SeasonClaimable(e)) Ui.Dot(pass.transform);
            Ui.Size(pass.gameObject, 230, 64);

            RectTransform head = Ui.Rect(card.transform, "ChestHead");
            Ui.Size(head.gameObject, -1, 34);
            Ui.Row(head, 10, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Icon(head, "chest", 32);
            Ui.Line(head, Loc.Tl("내 상자"), 26, Ui.Ink, TextAlignmentOptions.Left, true);
            TextMeshProUGUI hint = Ui.Line(head, Loc.Tl("한 번에 하나씩 열려요"), 20, Ui.Ink2, TextAlignmentOptions.Right, true);
            Ui.Size(hint.gameObject, -1, -1, 1);

            RectTransform row = Ui.Rect(card.transform, "Chests");
            Ui.Size(row.gameObject, -1, 164);
            HorizontalLayoutGroup g = Ui.Row(row, 10);
            g.childForceExpandWidth = true;
            long now = NowMs;
            for (int i = 0; i < 4; i++) ChestSlotView(row, e, i, now);
        }

        void ChestSlotView(Transform row, EconState e, int i, long now)
        {
            ChestStatus st = LobbyRules.Status(e, i, now);
            m_chestShown[i] = st;
            int slot = i;
            Button b = Ui.Button(row, st == ChestStatus.Ready ? "btn_gold" : "wood_tight", null, 0, () => TapChest(slot), 1.8f);
            Ui.Size(b.gameObject, -1, 164, 1);
            Ui.Column(b.transform, 2, 6, 6, 10, 12, TextAnchor.MiddleCenter);
            ChestSlot c = e.Chests[i];
            Image art = Ui.Image(b.transform, m_catalog.Frame("chest"), Color.white, "Art");
            art.preserveAspect = true;
            Ui.Size(art.gameObject, -1, 56);
            if (c == null)
            {
                art.color = new Color(1, 1, 1, 0.35f);
                b.interactable = false;
                Ui.Text(b.transform, Loc.Tl("대전 승리 시<br>획득"), 18, Ui.Cream2);
                return;
            }
            ChestDef d = Chests.Get(c.Kind);
            art.color = Color.Lerp(Color.white, Ui.Hex(d.Color), 0.55f);
            Ui.Line(b.transform, Loc.Tl(d.Kr), 20, Ui.Cream);
            long left = LobbyRules.LeftMs(e, i, now);
            switch (st)
            {
                case ChestStatus.Ready:
                    Ui.Line(b.transform, Loc.Tl("<b class=\"open\">열기!</b>"), 26, Ui.Cream);
                    break;
                case ChestStatus.Opening:
                    m_chestClock[i] = Ui.Line(b.transform, LobbyRules.ClockText(left), 22, Ui.Gold);
                    Ui.Line(b.transform, Loc.Icon("gem") + Chests.OpenCost(left), 18, Ui.Gem);
                    break;
                default:
                    Ui.Line(b.transform, LobbyRules.LeftText(left), 20, Ui.Cream);
                    Ui.Line(b.transform, LobbyRules.OpeningSlot(e) < 0 ? Loc.Tl("탭해서 열기") : Loc.Tl("차례 대기"), 16, Ui.Cream2);
                    break;
            }
        }

        void ModeRow(Transform body, EconState e)
        {
            RectTransform row = Ui.Rect(body, "Modes");
            Ui.Size(row.gameObject, -1, 132);
            HorizontalLayoutGroup g = Ui.Row(row, 16);
            g.childForceExpandWidth = true;

            bool allCleared = true;
            foreach (StageDef s in Campaign.Stages) if (!Progress.StageCleared(e, s.Id)) { allCleared = false; break; }
            string next = Progress.NextStageId(e);
            m_guideTargets["campaign"] = ModeCard(row, "btn_green", "map", Loc.Tl("캠페인"),
                allCleared ? Loc.Tl("{0} 모두 클리어", Loc.Icon("check")) : Loc.Tl("스테이지 {0}", next), false,
                m_campaign.OpenStages);
            if (Progress.FeatureUnlocked(e, "endless"))
                m_guideTargets["endless"] = ModeCard(row, "btn_purple", "infinity", Loc.Tl("무한 모드"), Loc.Tl("최고 {0}웨이브", LocalMeta.Data.BestWave),
                    LobbyRules.FeatureNew(e, LocalMeta.Data.SeenFeatures, "endless"),
                    () => { LocalMeta.MarkSeen("endless"); m_campaign.OpenEndless(); m_dirty = true; });
        }

        GameObject ModeCard(Transform row, string frame, string icon, string title, string desc, bool isNew, Action click)
        {
            Button b = Ui.Button(row, frame, null, 0, click);
            Ui.Size(b.gameObject, -1, 132, 1);
            Ui.Column(b.transform, 0, 10, 10, 6, 14, TextAnchor.MiddleCenter);
            Ui.Icon(b.transform, icon, 40);
            Ui.Line(b.transform, title, 30, Ui.Cream);
            Ui.Line(b.transform, desc, 20, Ui.Cream2);
            if (isNew) Ui.NewTag(b.transform);
            return b.gameObject;
        }

        void Nav(Transform parent, EconState e)
        {
            Image nav = Ui.Frame(parent, "wood", 1.6f, "Nav");
            Ui.Size(nav.gameObject, -1, 140);
            HorizontalLayoutGroup g = Ui.Row(nav.transform, 4, 18, 18, 18, 26);
            g.childForceExpandWidth = true;
            List<string> seen = LocalMeta.Data.SeenFeatures;

            NavButton(nav.transform, "shop", Loc.Tl("상점"), false, false, OpenShop);
            if (Progress.FeatureUnlocked(e, "units"))
            {
                string dot = LobbyRules.UnitsDotKey(e);
                m_guideTargets["units"] = NavButton(nav.transform, "helmet", Loc.Tl("덱"), LobbyRules.FeatureNew(e, seen, "units"), dot != null && !seen.Contains(dot),
                    () => { LocalMeta.MarkSeen("units"); if (dot != null) LocalMeta.MarkSeen(dot); m_deck.Open(); });
            }
            if (Progress.FeatureUnlocked(e, "research"))
                m_guideTargets["research"] = NavButton(nav.transform, "flask", Loc.Tl("연구"), LobbyRules.FeatureNew(e, seen, "research"), false,
                    () => { LocalMeta.MarkSeen("research"); m_research.Open(); });
            if (Progress.FeatureUnlocked(e, "quests"))
                m_guideTargets["quests"] = NavButton(nav.transform, "scroll", Loc.Tl("퀘스트"), LobbyRules.FeatureNew(e, seen, "quests"),
                    MetaRules.QuestClaimable(e, LocalMeta.Data.Daily.Progress, LocalMeta.Today), // 공짜 보상이라 받을 때까지 계속
                    () => { LocalMeta.MarkSeen("quests"); m_quests.Open(); });
            if (Progress.FeatureUnlocked(e, "pvp"))
            {
                NavButton(nav.transform, "trophy", Loc.Tl("로드"), false, false, m_road.Open);
                NavButton(nav.transform, "medal", Loc.Tl("랭킹"), false, false, m_rank.Open);
            }
        }

        GameObject NavButton(Transform nav, string icon, string label, bool isNew, bool dot, Action click)
        {
            Button b = Ui.Hit(nav, () => { click(); m_dirty = true; }, label);
            Ui.Size(b.gameObject, -1, 96, 1);
            Ui.Column(b.transform, 0, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Ui.Icon(b.transform, icon, 52);
            Ui.Line(b.transform, label, 22, Ui.Cream);
            if (isNew) Ui.NewTag(b.transform);
            else if (dot) Ui.Dot(b.transform);
            return b.gameObject;
        }

        void OpenShop() => m_shop.Open();

        /// <summary>월정액 오늘의 보석 (menu.ts notice)</summary>
        void MonthlyNotice(Transform body)
        {
            Image row = Ui.Frame(body, "paper_dark", 1.6f, "Monthly");
            Ui.Size(row.gameObject, -1, 80);
            Ui.Row(row.transform, 12, 20, 14, 8, 10);
            TextMeshProUGUI t = Ui.Line(row.transform, Loc.Icon("calendar") + " " + Loc.Tl("월정액 오늘의 보석"), 24, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Size(t.gameObject, -1, -1, 1);
            Ui.Size(Ui.Button(row.transform, "btn_green", Loc.Tl("받기") + " " + Loc.Icon("gem"), 22, ClaimMonthly).gameObject, 150, 62);
        }

        async void ClaimMonthly()
        {
            int n = await Server<int>(CloudApi.ClaimMonthly, null);
            if (n <= 0) return;
            m_audio.Play(Sfx.Coin);
            m_toast.Show(Loc.Tl("월정액 보석 +{0} {1}", n, Loc.Icon("gem")));
        }

        void Launch(BattleLaunch l)
        {
            if (m_busy) return;
            Hide();
            Play?.Invoke(l);
        }

        /// <summary>결과의 '다시 대전': 방금 받은 상자·로드 공개를 다 본 뒤 상대 찾기 (매칭 창이 공개를 가리거나 끊지 않게)</summary>
        public void QuickMatchAfterReveals() => m_reveal.WhenDone(QuickMatch);

        /// <summary>대전 시작 (로비 버튼 · 결과의 '다시 대전'): 상대 찾기 → 대전</summary>
        public void QuickMatch()
        {
            if (m_busy || m_match.Busy) return;
            m_match.Start(EconCache.State, opp => Launch(BattleLaunch.Pvp(opp)));
        }

        /// <summary>결과 화면 → 스테이지 정보 (다음 스테이지 / 다시 하기)</summary>
        public void OpenStageInfo(string id) => m_campaign.OpenStageInfo(id);

        /// <summary>덱 편집 (5단계 덱 페이지가 연결)</summary>
        public CampaignPages Pages => m_campaign;

        // ======================= 상자 (chests.ts tapChest) =======================
        async void TapChest(int i)
        {
            if (m_busy) return;
            EconState e = EconCache.State;
            long now = NowMs;
            ChestStatus st = LobbyRules.Status(e, i, now);
            if (st == ChestStatus.Empty) return;
            if (st == ChestStatus.Ready) { ShowReveal(await Server<Reveal>(CloudApi.OpenSlot, CloudClient.Args("slot", i, "pay", false))); return; }
            if (st == ChestStatus.Locked && LobbyRules.OpeningSlot(e) < 0)
            {
                await Server<bool>(CloudApi.StartUnlock, CloudClient.Args("slot", i));
                return;
            }
            int cost = Chests.OpenCost(LobbyRules.LeftMs(e, i, now));
            bool waiting = st == ChestStatus.Locked;
            string kr = Loc.Tl(Chests.Get(e.Chests[i].Kind).Kr);
            string msg = waiting
                ? Loc.Tl("상자는 <b>한 번에 하나씩</b>만 열 수 있어요.<br>지금 여는 상자가 끝나면 이 상자를 열 수 있어요.<br><br>{0}를 {1}{2}로 바로 열까요?", kr, Loc.Icon("gem"), cost)
                : Loc.Tl("{0}를 {1}{2}로 바로 열까요?", kr, Loc.Icon("gem"), cost);
            m_modal.Confirm(waiting ? Loc.Tl("상자 대기 중") : Loc.Tl("바로 열기"), msg, Loc.Tl("확인"), async ok =>
            {
                if (!ok) return;
                if (EconCache.State.Crystals < cost) { Offers.Shortage(cost - EconCache.State.Crystals); return; }
                ShowReveal(await Server<Reveal>(CloudApi.OpenSlot, CloudClient.Args("slot", i, "pay", true)));
            });
        }

        /// <summary>카드 공개 연출 (쌓이면 차례로)</summary>
        public void ShowReveal(Reveal r) => m_reveal.Show(r, () => m_dirty = true);

        /// <summary>도달했지만 안 받은 로드 보상은 서버에 받기를 요청한다 (menu.ts render 앞부분)</summary>
        async void ClaimRoadIfNeeded(EconState e)
        {
            if (m_roadBusy || !EconCache.Loaded || !LobbyRules.RoadUnclaimed(e)) return;
            m_roadBusy = true;
            try
            {
                RoadClaimResult r = await CloudClient.Econ<RoadClaimResult>(CloudApi.ClaimRoad);
                if (r == null || r.Steps.Count == 0) return;
                var parts = new List<string>();
                foreach (RoadStep s in r.Steps) parts.Add(RoadText(s));
                m_toast.Show(Loc.Tl("트로피 로드 보상: {0}", string.Join(" · ", parts)));
                foreach (Reveal rv in r.Reveals) ShowReveal(rv);
            }
            catch (Exception ex) { Debug.LogWarning("[Lobby] ClaimRoad 실패: " + ex.Message); }
            finally { m_roadBusy = false; }
        }

        /// <summary>서버 경제 함수 (두 번 누르기 막음, 실패하면 안내)</summary>
        async Task<T> Server<T>(string fn, Dictionary<string, object> args)
        {
            m_busy = true;
            try
            {
                T r = await CloudClient.Econ<T>(fn, args);
                return r;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Lobby] " + fn + " 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
                return default;
            }
            finally { m_busy = false; }
        }

        /// <summary>로드 보상 글 (menu.ts roadRewardText)</summary>
        public static string RoadText(RoadStep s)
        {
            if (s.Unit != null) return Loc.Tl(Units.Get(s.Unit.Value).Kr);
            if (s.Attack != null) return Loc.Tl("몹 카드 {0}", Loc.Tl(Attacks.Get(s.Attack.Value).Kr));
            if (s.Chest != null) return Loc.Tl(Chests.Get(s.Chest.Value).Kr);
            return Loc.Icon("gem") + s.Crystals;
        }
    }
}
