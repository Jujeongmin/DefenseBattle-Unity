using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 상점 (menu.ts openShop): 보석·보유 아이템, 무료 광고 뽑기권, 한정 특가, 유닛 뽑기(1회·10회, 뽑기권 먼저 + 모자란 만큼 보석),
    /// 상자·추천 패키지·유닛 패키지·시즌 패스·부스터·보석 상품, 확률 정보. 결제는 ProductBuy (스토어 연결 전엔 '준비 중').
    /// </summary>
    public sealed class ShopPage
    {
        readonly Modal m_panel;
        readonly Modal m_modal;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly RevealPopup m_reveal;
        readonly ProductBuy m_buy;
        readonly OfferPopup m_offers;
        readonly Action m_closed;
        readonly List<ProductDef> m_active = new List<ProductDef>();
        bool m_busy;

        public ShopPage(Modal panel, Modal modal, Toast toast, BattleAudio audio, RevealPopup reveal, ProductBuy buy, OfferPopup offers, Action closed)
        {
            m_panel = panel;
            m_modal = modal;
            m_toast = toast;
            m_audio = audio;
            m_reveal = reveal;
            m_buy = buy;
            m_offers = offers;
            m_closed = closed;
        }

        public void Open() => Draw();

        void Draw()
        {
            EconState e = EconCache.State;
            RectTransform body = m_panel.Panel(Loc.Tl("상점"), m_closed);
            Ui.Line(body, Loc.Icon("gem") + " " + Ui.Num(e.Crystals), 34, Ui.Ink, TextAlignmentOptions.Center, true);
            string items = Loc.Icon("ticket") + e.Item(ItemKey.GachaTicket) + " · " + Loc.Icon("bag") + e.Item(ItemKey.GoldBooster);
            Ui.Line(body, Loc.Tl("보유: {0}", items), 22, Ui.Ink2, TextAlignmentOptions.Center, true);

            if (RewardAds.Service.IsReady) FreeTickets(body, e);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            OfferRules.ActiveOffers(LocalMeta.Data.Offers, now, m_active);
            if (m_active.Count > 0)
            {
                Section(body, Loc.Icon("timer") + " " + Loc.Tl("한정 특가"), null, false);
                foreach (ProductDef o in m_active) Card(body, e, o.Id, true, now);
            }

            Section(body, Loc.Tl("유닛 뽑기"), Loc.Tl("카드로 해금·레벨업"), true);
            RectTransform row = Ui.Rect(body, "Gacha");
            Ui.Size(row.gameObject, -1, 130);
            HorizontalLayoutGroup g = Ui.Row(row, 12);
            g.childForceExpandWidth = true;
            int tix = e.Item(ItemKey.GachaTicket);
            GachaButton(row, 1, "btn_blue", tix);
            GachaButton(row, 10, "btn_gold", tix);
            var rates = new System.Text.StringBuilder();
            for (int k = 0; k < Chests.GachaRates.Length; k++)
            {
                if (k > 0) rates.Append(" · ");
                rates.Append("<color=#").Append(Units.RarityColor[k].ToString("x6")).Append('>').Append(Loc.Tl(Units.RarityKr[k]))
                    .Append(' ').Append(Mathf.RoundToInt((float)(Chests.GachaRates[k] * 100))).Append("%</color>");
            }
            Ui.Line(body, rates.ToString(), 22, Ui.Ink2, TextAlignmentOptions.Center, true);

            Section(body, Loc.Tl("상자"), null, true);
            Card(body, e, "chest_gold", false, now);
            Card(body, e, "chest_magic", false, now);
            Card(body, e, "chest_legend", true, now);
            Section(body, Loc.Tl("추천 패키지"), null, false);
            if (!OfferRules.ProductOwned(e, "starter_pack", e.Season.Id)) Card(body, e, "starter_pack", true, now);
            Card(body, e, "monthly_pass", true, now);
            Card(body, e, "ad_remove", false, now);
            bool packs = false;
            foreach (KeyValuePair<Job, string> kv in Products.UnitPacks)
            {
                string pack = OfferRules.UnitPackAvailable(e, LocalMeta.Data.OfferShownAt, kv.Key, e.Season.Id);
                if (pack == null) continue;
                if (!packs) Section(body, Loc.Tl("유닛 패키지"), Loc.Tl("바로 해금 + Lv5"), false);
                packs = true;
                Card(body, e, pack, false, now);
            }
            Section(body, Loc.Tl("PvP 시즌 패스"), null, false);
            Card(body, e, "season_premium", false, now);
            Section(body, Loc.Tl("부스터"), Loc.Tl("캠페인·무한 모드 전용"), false);
            Card(body, e, "gold_booster_pack", false, now);
            Section(body, Loc.Tl("보석"), null, false);
            GemGrid(body);
        }

        /// <summary>무료: 광고 보고 뽑기권 (서버 AdReward(false) - 하루 횟수는 서버가 센다)</summary>
        void FreeTickets(Transform body, EconState e)
        {
            e.Counters.TryGetValue("adTicket:" + LocalMeta.Today, out int used);
            int left = Math.Max(0, EconRules.AdTicketsPerDay - used);
            Section(body, Loc.Tl("무료"), null, false);
            Image row = Ui.Frame(body, "paper_dark", 1.8f, "Free");
            Ui.Size(row.gameObject, -1, 110);
            Ui.Row(row.transform, 14, 20, 16, 10, 12);
            Image tv = Ui.Icon(row.transform, "tv", 52);
            if (left > 0) Ui.Dot(tv.transform);
            RectTransform info = Ui.Rect(row.transform, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 2, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(info, Loc.Tl("광고 보고 뽑기권 {0}장", EconRules.AdTicketAmount), 26, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(info, Loc.Tl("오늘 {0}회 남음", left), 20, Ui.Cream2, TextAlignmentOptions.Left);
            Button b = Ui.Button(row.transform, "btn_blue", Loc.Tl("받기"), 24, AdTicket);
            b.interactable = left > 0;
            Ui.Size(b.gameObject, 150, 72);
        }

        void AdTicket()
        {
            if (m_busy) return;
            m_busy = true;
            RewardAds.Service.Show(async ok =>
            {
                int n = 0;
                if (ok)
                {
                    try { n = await CloudClient.Econ<int>(CloudApi.AdReward, CloudClient.Args("doubleReward", false)); }
                    catch (Exception ex) { Debug.LogWarning("[Shop] AdReward 실패: " + ex.Message); }
                }
                m_busy = false;
                if (n > 0)
                {
                    m_audio.Play(Sfx.Coin);
                    m_toast.Show(Loc.Tl("뽑기권 +{0} {1}", n, Loc.Icon("ticket")));
                }
                Draw();
            });
        }

        /// <summary>상품 카드 한 줄 (그림 · 이름·배지·설명 · 가격 버튼). hot = 눈에 띄게</summary>
        void Card(Transform body, EconState e, string id, bool hot, long now)
        {
            ProductDef p = Products.Get(id);
            bool owned = OfferRules.ProductOwned(e, id, e.Season.Id);
            Image card = Ui.Frame(body, hot ? "btn_purple" : "paper_dark", 1.8f, id);
            Ui.Size(card.gameObject, -1, 140);
            Ui.Row(card.transform, 12, 14, 16, 10, 10);
            if (!owned) ProductUi.EffBadge(card.transform, p);
            ProductUi.Picture(card.transform, p, 104);

            RectTransform info = Ui.Rect(card.transform, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            string title = Loc.Tl(p.Kr);
            if (p.Badge != null) title += "  <size=70%><color=#ffc928>" + Loc.Tl(p.Badge) + "</color></size>";
            if (id == "monthly_pass" && MetaRules.MonthlyActive(e, now))
                title += "  <size=70%><color=#7cfc7c>" + Loc.Tl("{0}일 남음", MetaRules.MonthlyDaysLeft(e, now)) + "</color></size>";
            if (p.Category == "offer")
            {
                long ms = OfferRules.MsLeft(LocalMeta.Data.Offers, id, now);
                title += "  <size=70%><color=#ffc928>" + Loc.Icon("timer") + Loc.Tl("{0}시간 {1}분", ms / 3_600_000L, ms % 3_600_000L / 60_000L) + "</color></size>";
            }
            Ui.Line(info, title, 26, Ui.Cream, TextAlignmentOptions.Left);
            string desc = p.Desc.Length == 0 ? "" : p.DescArg != null ? Loc.Tl(p.Desc, p.DescArg) : Loc.Tl(p.Desc);
            Ui.Text(info, desc, 19, Ui.Cream2, TextAlignmentOptions.Left);

            Button b = Ui.Button(card.transform, owned ? "btn_gray" : "btn_gold", owned ? Loc.Tl("보유 {0}", Loc.Icon("check")) : Store.Price(p), 24,
                () => m_buy.Buy(id, _ => Draw()));
            b.interactable = !owned;
            Ui.Size(b.gameObject, 160, 76);
        }

        /// <summary>보석 상품 3칸 격자</summary>
        void GemGrid(Transform body)
        {
            RectTransform grid = Ui.Rect(body, "Gems");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(196, 236);
            g.spacing = new Vector2(12, 12);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            g.childAlignment = TextAnchor.UpperCenter;
            foreach (ProductDef p in Products.All)
            {
                if (p.Category != "crystal") continue;
                string id = p.Id;
                Image cell = Ui.Frame(grid, "paper_dark", 1.8f, id);
                Ui.Column(cell.transform, 4, 10, 10, 18, 12, TextAnchor.UpperCenter);
                ProductUi.EffBadge(cell.transform, p);
                RectTransform pic = Ui.Rect(cell.transform, "PicRow");
                Ui.Size(pic.gameObject, -1, 96);
                Ui.Row(pic, 0);
                ProductUi.Picture(pic, p, 96);
                Ui.Line(cell.transform, Loc.Tl(p.Kr), 22, Ui.Cream);
                Ui.Size(Ui.Button(cell.transform, "btn_gold", Store.Price(p), 22, () => m_buy.Buy(id, _ => Draw())).gameObject, -1, 64);
            }
        }

        /// <summary>제목 줄 (+ 확률 정보 버튼)</summary>
        public void Section(Transform body, string title, string note, bool odds)
        {
            RectTransform head = Ui.Rect(body, "Section");
            Ui.Size(head.gameObject, -1, 60);
            Ui.Row(head, 10, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(head, title, 30, Ui.Ink, TextAlignmentOptions.Left, true);
            TextMeshProUGUI n = Ui.Line(head, note ?? "", 20, Ui.Ink2, TextAlignmentOptions.Left, true);
            Ui.Size(n.gameObject, -1, -1, 1);
            if (odds) Ui.Size(Ui.Button(head, "btn_gray", Loc.Tl("확률 정보"), 20, OpenOdds).gameObject, 170, 56);
        }

        void GachaButton(Transform row, int n, string frame, int tix)
        {
            // 가진 뽑기권을 먼저 쓰고 모자란 만큼 보석 (예: 뽑기권 3 + 보석 630)
            (int useTix, int gems) = ShopRules.GachaCost(tix, n);
            string cost = (useTix > 0 ? Loc.Icon("ticket") + useTix : "") + (useTix > 0 && gems > 0 ? " + " : "") + (gems > 0 ? Loc.Icon("gem") + gems : "");
            Button b = Ui.Button(row, frame, null, 0, () => Gacha(n));
            Ui.Size(b.gameObject, -1, 130, 1);
            Ui.Column(b.transform, 0, 8, 8, 10, 18, TextAnchor.MiddleCenter);
            Ui.Line(b.transform, Loc.Tl(n == 1 ? "1회" : "10회"), 32, Ui.Cream);
            Ui.Line(b.transform, cost + (n == 1 && tix >= 1 ? " " + Loc.Tl("(보유 {0})", tix) : ""), 22, Ui.Cream);
            if (n == 10) Ui.Line(b.transform, Loc.Tl("영웅+ 보장"), 18, Ui.Cream2);
        }

        async void Gacha(int n)
        {
            if (m_busy) return;
            EconState e = EconCache.State;
            (int useTix, int gems) = ShopRules.GachaCost(e.Item(ItemKey.GachaTicket), n);
            if (e.Crystals < gems) { m_offers.Shortage(gems - e.Crystals); return; }
            m_busy = true;
            Reveal r = null;
            try { r = await CloudClient.Econ<Reveal>(CloudApi.Gacha, CloudClient.Args("count", n, "ticket", useTix > 0)); }
            catch (Exception ex)
            {
                Debug.LogWarning("[Shop] Gacha 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
            }
            finally { m_busy = false; }
            Draw();
            if (r != null) m_reveal.Show(r, Draw);
        }

        /// <summary>확률 정보 (menu.ts openOdds - 규칙은 서버 Gacha / RollChest 와 같다)</summary>
        public void OpenOdds()
        {
            RectTransform body = m_modal.Custom(Loc.Tl("확률 정보"));
            Ui.Line(body, Loc.Tl("유닛 뽑기"), 28, Ui.Ink, TextAlignmentOptions.Left, true);
            Table(body, new[] { Loc.Tl("등급"), Loc.Tl("확률"), Loc.Tl("카드"), Loc.Tl("유닛 수"), Loc.Tl("유닛 1종") }, rows =>
            {
                for (int k = 0; k < Chests.GachaRates.Length; k++)
                {
                    int pool = ShopRules.PoolSize((Rarity)k);
                    rows.Add(new[]
                    {
                        "<color=#" + Units.RarityColor[k].ToString("x6") + ">" + Loc.Tl(Units.RarityKr[k]) + "</color>",
                        ShopRules.Pct(Chests.GachaRates[k]), Loc.Tl("{0}장", Chests.GachaCount[k]), Loc.Tl("{0}종", pool),
                        ShopRules.Pct(Chests.GachaRates[k] / pool),
                    });
                }
            });
            Ui.Text(body, Loc.Tl("· 1회마다 위 확률로 등급을 정하고, 그 등급 유닛 중 하나가 같은 확률로 나와요.") + "\n"
                + Loc.Tl("· 10회 뽑기에서 영웅·전설이 하나도 없으면 마지막 1회가 영웅(영웅 {0}종 중 무작위)으로 바뀌어요.", ShopRules.PoolSize(Rarity.Epic)),
                18, Ui.Ink2, TextAlignmentOptions.Left, true);

            int a = Arenas.IndexOf(EconCache.State.Pvp.Trophies);
            Ui.Line(body, Loc.Tl("상자") + "  <size=70%>" + Loc.Tl("지금 아레나({0}) 기준", Loc.Tl(Arenas.All[a].Kr)) + "</size>", 28, Ui.Ink, TextAlignmentOptions.Left, true);
            Table(body, new[] { Loc.Tl("상자"), Loc.Tl("카드"), Loc.Tl("희귀"), Loc.Tl("영웅"), Loc.Tl("전설"), Loc.Icon("gem") }, rows =>
            {
                foreach (ChestDef d in Chests.All)
                {
                    ShopRules.ChestOdds o = ShopRules.ChestRow(d, a);
                    string epic = o.EpicChance == 0 ? Loc.Tl("{0}장", o.EpicBase)
                        : o.EpicBase > 0 ? Loc.Tl("{0}장 +{1}", o.EpicBase, ShopRules.Pct(o.EpicChance)) : Loc.Tl("{0}로 1장", ShopRules.Pct(o.EpicChance));
                    string legend = o.Legendary >= 1 ? Loc.Tl("1장") : o.Legendary > 0 ? Loc.Tl("{0}로 1장", ShopRules.Pct(o.Legendary)) : "-";
                    rows.Add(new[] { Loc.Tl(d.Kr), Loc.Tl("{0}장", o.Total), Loc.Tl("{0}장", o.Rare), epic, legend, d.GemsMin + "~" + d.GemsMax });
                }
            });
            Ui.Text(body, Loc.Tl("· 나머지 카드는 일반이에요. 아레나가 오를수록 카드 장수와 희귀 카드가 늘어요.") + "\n"
                + Loc.Tl("· 유닛은 같은 등급 안에서 무작위로 정해지고, 일반은 3종·희귀는 2종에 나눠 담겨요. 보석은 범위 안에서 같은 확률이에요.") + "\n"
                + Loc.Tl("· 금·마법·전설 상자는 상점에서 사면 바로 열려요. 나무·은 상자는 대전 승리로만 얻어요."),
                18, Ui.Ink2, TextAlignmentOptions.Left, true);
            Ui.Size(Ui.Button(body, "btn_red", Loc.Tl("확인"), 30, m_modal.Close).gameObject, -1, 88);
        }

        static void Table(Transform body, string[] head, Action<List<string[]>> fill)
        {
            var rows = new List<string[]>();
            fill(rows);
            RectTransform grid = Ui.Rect(body, "Table");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(540f / head.Length, 40);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = head.Length;
            foreach (string h in head) Cell(grid, h, Ui.Ink, true);
            foreach (string[] r in rows) foreach (string c in r) Cell(grid, c, Ui.Ink2, false);
        }

        static void Cell(Transform grid, string text, Color color, bool head)
        {
            Image bg = Ui.Image(grid, null, head ? new Color(0, 0, 0, 0.12f) : new Color(0, 0, 0, 0.04f), "Cell");
            TextMeshProUGUI t = Ui.Line(bg.transform, text, head ? 20 : 18, color, TextAlignmentOptions.Center, true);
            Ui.Fill(t.rectTransform, 2, 0, 2, 0);
        }
    }
}
