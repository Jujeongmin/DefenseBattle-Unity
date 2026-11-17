using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 시즌 패스 (menu.ts openPvp('pass')): 지금 단계·다음 단계까지 XP, 프리미엄 구매(상점 결제),
    /// 단계마다 카드 (위 프리미엄 / 아래 무료) 를 옆으로 넘기는 띠 - 열면 지금 단계로. 받기는 서버 ClaimSeason.
    /// </summary>
    public sealed class PassPage
    {
        readonly Modal m_panel;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly Action m_closed;
        float m_scroll = -1;
        bool m_busy;

        /// <summary>프리미엄 패스 구매 (상점 결제 - 8단계·5단계)</summary>
        public Action BuyPremium;

        public PassPage(Modal panel, Toast toast, BattleAudio audio, Action closed)
        {
            m_panel = panel;
            m_toast = toast;
            m_audio = audio;
            m_closed = closed;
        }

        public void Open()
        {
            m_scroll = -1;
            Draw();
        }

        public static string RewardText(SeasonReward r)
        {
            string s = "";
            if (r.Crystals > 0) s += Loc.Icon("gem") + r.Crystals;
            if (r.GachaTickets > 0) s += (s.Length > 0 ? " " : "") + Loc.Icon("ticket") + r.GachaTickets;
            if (r.GoldBoosters > 0) s += (s.Length > 0 ? " " : "") + Loc.Icon("bag") + r.GoldBoosters;
            return s;
        }

        static string BigIcon(SeasonReward r) => r.GachaTickets > 0 ? "ticket" : r.GoldBoosters > 0 ? "bag" : "gem";

        void Draw()
        {
            EconState e = EconCache.State;
            int t = EconRules.SeasonTierOf(e);
            int inTier = e.Season.Xp % Season.XpPerTier;
            RectTransform body = m_panel.Panel(Loc.Tl("시즌 패스"), m_closed);

            RectTransform head = Ui.Rect(body, "Head");
            Ui.Size(head.gameObject, -1, 90);
            Ui.Row(head, 14, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Image lvl = Ui.Frame(head, "round_red", 1.4f, "Level");
            Ui.Size(lvl.gameObject, 84, 84);
            Ui.Fill(Ui.Line(lvl.transform, t.ToString(), 36, Ui.Cream).rectTransform);
            RectTransform info = Ui.Rect(head, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 6, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            string sid = e.Season.Id.Length > 1 ? e.Season.Id.Substring(1) : e.Season.Id;
            Ui.Line(info, Loc.Tl("시즌") + " " + sid + " " + Loc.Tl("· 다음 단계까지") + " " + (Season.XpPerTier - inTier) + " XP", 22, Ui.Ink2, TextAlignmentOptions.Left, true);
            Image bar = Ui.Image(info, null, new Color(0, 0, 0, 0.3f), "Bar");
            Ui.Size(bar.gameObject, -1, 20);
            Image fill = Ui.Image(bar.transform, null, Ui.Gold, "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(t >= Season.TierCount ? 1 : inTier / (float)Season.XpPerTier, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            if (!e.Season.Premium)
                Ui.Size(Ui.Button(body, "btn_gold", Loc.Icon("trophy") + " " + Loc.Tl("프리미엄 패스 구매"), 30, () =>
                {
                    if (BuyPremium != null) BuyPremium();
                    else m_toast.Show(Loc.Tl("준비 중"));
                }).gameObject, -1, 92);

            // 옆으로 넘기는 띠
            RectTransform view = Ui.Rect(body, "Track");
            Ui.Size(view.gameObject, -1, 470);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = Ui.Rect(view, "Content");
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            HorizontalLayoutGroup hg = Ui.Row(content, 12, 4, 4, 4, 4);
            hg.childControlHeight = true;
            hg.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            const float cardW = 176, gap = 12;
            for (int n = 1; n <= Season.TierCount; n++) Tier(content, e, n, t, cardW);

            Ui.Text(body, Loc.Tl("옆으로 넘겨 보세요 · 대전에서 이기면 XP"), 20, Ui.Ink2, TextAlignmentOptions.Center, true);

            // 처음 열면 지금 단계를 가운데로, 다시 그리면(받기) 보던 위치 그대로
            float target = m_scroll >= 0 ? m_scroll : Mathf.Max(0, (Mathf.Max(1, t) - 1) * (cardW + gap) - (620 - cardW) / 2f);
            Canvas.ForceUpdateCanvases();
            content.anchoredPosition = new Vector2(-target, 0);
            scroll.onValueChanged.AddListener(_ => m_scroll = -content.anchoredPosition.x);
        }

        void Tier(Transform content, EconState e, int n, int t, float w)
        {
            bool reached = n <= t;
            Image col = Ui.Frame(content, n == Mathf.Max(1, t) ? "btn_gold" : "paper_dark", 1.8f, "Tier" + n);
            Ui.Size(col.gameObject, w, -1);
            Ui.Column(col.transform, 6, 8, 8, 8, 12);
            Ui.Line(col.transform, n.ToString(), 28, Ui.Cream);
            string pCls = e.Season.ClaimedPremium.Contains(n) ? "done" : reached && e.Season.Premium ? "ready" : "lock";
            string fCls = e.Season.ClaimedFree.Contains(n) ? "done" : reached ? "ready" : "lock";
            Cell(col.transform, Season.Premium(n), n, true, pCls);
            Cell(col.transform, Season.Free(n), n, false, fCls);
        }

        void Cell(Transform parent, SeasonReward r, int n, bool premium, string cls)
        {
            Button b = Ui.Button(parent, cls == "ready" ? "btn_green" : premium ? "btn_purple" : "btn_blue", null, 0, () => Claim(n, premium), 1.8f);
            b.interactable = cls == "ready";
            Ui.Size(b.gameObject, -1, 196);
            Ui.Column(b.transform, 2, 6, 6, 8, 14, TextAnchor.MiddleCenter);
            Ui.Line(b.transform, premium ? Loc.Tl("프리미엄") : Loc.Tl("무료"), 18, Ui.Cream2);
            Ui.Icon(b.transform, BigIcon(r), 52);
            Ui.Line(b.transform, RewardText(r), 22, Ui.Cream);
            Ui.Line(b.transform, cls == "done" ? Loc.Tl("{0} 받음", Loc.Icon("check")) : cls == "ready" ? Loc.Tl("받기!") : Loc.Icon("lock"), 20, Ui.Cream);
        }

        async void Claim(int tier, bool premium)
        {
            if (m_busy) return;
            m_busy = true;
            try
            {
                if (await CloudClient.Econ<bool>(CloudApi.ClaimSeason, CloudClient.Args("tier", tier, "premium", premium)))
                {
                    m_audio.Play(Sfx.Coin);
                    m_toast.Show(Loc.Tl("시즌 보상 획득!"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Pass] ClaimSeason 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
            }
            finally { m_busy = false; }
            Draw();
        }
    }
}
