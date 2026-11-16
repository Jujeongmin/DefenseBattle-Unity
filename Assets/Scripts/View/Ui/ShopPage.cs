using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 상점 (menu.ts openShop): 보석·보유 아이템, 유닛 뽑기(1회·10회, 뽑기권 우선), 확률 정보.
    /// 상품(보석·패키지·상자 구매)은 8단계(상점·특가)에서 이 창에 붙는다.
    /// </summary>
    public sealed class ShopPage
    {
        readonly Modal m_panel;
        readonly Modal m_modal;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly RevealPopup m_reveal;
        readonly Action m_closed;
        bool m_busy;

        /// <summary>상품 칸 (8단계가 채운다)</summary>
        public Action<Transform> DrawProducts;

        public ShopPage(Modal panel, Modal modal, Toast toast, BattleAudio audio, RevealPopup reveal, Action closed)
        {
            m_panel = panel;
            m_modal = modal;
            m_toast = toast;
            m_audio = audio;
            m_reveal = reveal;
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

            DrawProducts?.Invoke(body);
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
            (bool ticket, int gems) = ShopRules.GachaPlan(tix, n);
            string cost = ticket ? Loc.Icon("ticket") + n : Loc.Icon("gem") + gems;
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
            (bool ticket, int gems) = ShopRules.GachaPlan(e.Item(ItemKey.GachaTicket), n);
            if (!ticket && e.Crystals < gems) { m_toast.Show(Loc.Tl("보석이 부족해요")); return; }
            m_busy = true;
            Reveal r = null;
            try { r = await CloudClient.Econ<Reveal>(CloudApi.Gacha, CloudClient.Args("count", n, "ticket", ticket)); }
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
