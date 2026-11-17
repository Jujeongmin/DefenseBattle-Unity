using System;
using DefenseBattle.Core;
using DefenseBattle.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 랭킹 (menu.ts openPvp('rank') · fetchRanking): 지난번 목록을 바로 그리고 새로 받아 바꾼다
    /// (서버 응답을 기다리는 동안 빈 창이 보이지 않게). 로비에 들어올 때 미리 한 번 받아 둔다.
    /// </summary>
    public sealed class RankPage
    {
        readonly Modal m_panel;
        readonly Action m_closed;
        LeaderboardReply m_last;
        bool m_fetching;
        RectTransform m_list;

        public RankPage(Modal panel, Action closed)
        {
            m_panel = panel;
            m_closed = closed;
        }

        public void Prefetch()
        {
            if (m_last == null) Fetch();
        }

        public void Open()
        {
            RectTransform body = m_panel.Panel(Loc.Tl("랭킹"), () => { m_list = null; m_closed?.Invoke(); });
            Ui.Line(body, Loc.Icon("calendar") + " " + Loc.Tl("매달 1일 초기화: {0} 넘는 트로피 절반", PvpConfig.SeasonResetAbove), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            m_list = Ui.Rect(body, "List");
            Ui.Column(m_list, 8);
            DrawList();
            Fetch();
        }

        async void Fetch()
        {
            if (m_fetching) return;
            m_fetching = true;
            try { m_last = await CloudClient.Call<LeaderboardReply>(CloudApi.Leaderboard); }
            catch (Exception ex) { Debug.LogWarning("[Rank] Leaderboard 실패: " + ex.Message); }
            finally { m_fetching = false; }
            if (m_list != null) DrawList();
        }

        void DrawList()
        {
            if (m_list == null) return;
            Ui.Clear(m_list);
            if (m_last == null)
            {
                Ui.Line(m_list, Loc.Tl("<div class=\"fine\">불러오는 중...</div>"), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
                return;
            }
            if (m_last.Top.Count == 0)
            {
                Ui.Text(m_list, Loc.Tl("<div class=\"fine\">아직 기록이 없습니다. 첫 승리를 차지하세요!</div>"), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
                return;
            }
            string me = UgsSession.PlayerId;
            for (int i = 0; i < m_last.Top.Count; i++)
            {
                RankEntry r = m_last.Top[i];
                bool mine = r.PlayerId == me;
                Image row = Ui.Frame(m_list, mine ? "btn_gold" : "paper_dark", 1.8f, "Row");
                Ui.Size(row.gameObject, -1, 76);
                Ui.Row(row.transform, 12, 18, 18, 6, 10, TextAnchor.MiddleLeft);
                if (i < 3) Ui.Icon(row.transform, i == 0 ? "medal" : i == 1 ? "medal_silver" : "medal_bronze", 44);
                else Ui.Size(Ui.Line(row.transform, (i + 1).ToString(), 26, Ui.Cream).gameObject, 44, -1);
                PvpTier tier = Arenas.Of(r.Trophies);
                TextMeshProUGUI nm = Ui.Line(row.transform, Escape(r.Nick) + "  <size=75%><color=#" + tier.Color.ToString("x6") + ">" + Loc.Tl(tier.Kr) + "</color></size>", 24, Ui.Cream, TextAlignmentOptions.Left);
                Ui.Size(nm.gameObject, -1, -1, 1);
                Ui.Line(row.transform, Loc.Icon("trophy") + " " + r.Trophies, 24, Ui.Gold, TextAlignmentOptions.Right);
            }
        }

        /// <summary>남이 정한 닉네임의 TMP 태그를 막는다</summary>
        static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";
    }
}
