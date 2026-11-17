using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>프로필 (menu.ts openProfile): 닉네임(서버 SetNick), 티어·전적, 캠페인 별, 무한 최고 웨이브</summary>
    public sealed class ProfilePage
    {
        readonly Modal m_panel;
        readonly Toast m_toast;
        readonly Action m_closed;
        bool m_busy;

        public ProfilePage(Modal panel, Toast toast, Action closed)
        {
            m_panel = panel;
            m_toast = toast;
            m_closed = closed;
        }

        public void Open()
        {
            EconState e = EconCache.State;
            PvpStats p = e.Pvp;
            PvpTier tier = Arenas.Of(p.Trophies);
            int stars = 0;
            foreach (StageDef s in Campaign.Stages) stars += e.StarsOf(s.Id);
            RectTransform body = m_panel.Panel(Loc.Tl("프로필"), m_closed);

            Image nickRow = Ui.Frame(body, "paper_dark", 1.8f, "Nick");
            Ui.Size(nickRow.gameObject, -1, 130);
            Ui.Row(nickRow.transform, 12, 20, 20, 12, 14);
            RectTransform info = Ui.Rect(nickRow.transform, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(info, Loc.Tl("닉네임"), 26, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(info, Loc.Tl("PvP 상대와 랭킹에 표시됩니다"), 18, Ui.Cream2, TextAlignmentOptions.Left);
            TMP_InputField input = Ui.Input(nickRow.transform, Profile.Nick, 12);
            Ui.Size(input.gameObject, 260, 64);

            Image tierRow = Ui.Frame(body, "paper_dark", 1.8f, "Tier");
            Ui.Size(tierRow.gameObject, -1, 110);
            Ui.Column(tierRow.transform, 4, 20, 20, 14, 16, TextAnchor.MiddleLeft);
            Ui.Line(tierRow.transform, Loc.Icon("trophy") + " " + Loc.Tl(tier.Kr) + " · " + p.Trophies, 28, Ui.Hex(tier.Color), TextAlignmentOptions.Left);
            Ui.Line(tierRow.transform, Loc.Tl("{0}승 {1}패 · 연승 {2}", p.Wins, p.Losses, p.Streak), 20, Ui.Cream2, TextAlignmentOptions.Left);

            Image campRow = Ui.Frame(body, "paper_dark", 1.8f, "Campaign");
            Ui.Size(campRow.gameObject, -1, 110);
            Ui.Column(campRow.transform, 4, 20, 20, 14, 16, TextAnchor.MiddleLeft);
            Ui.Line(campRow.transform, Loc.Icon("star") + " " + Loc.Tl("캠페인 별") + " " + stars + " / " + Campaign.Stages.Length * 3, 26, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(campRow.transform, Loc.Tl("무한 모드 최고 {0}웨이브", LocalMeta.Data.BestWave), 20, Ui.Cream2, TextAlignmentOptions.Left);

            Ui.Size(Ui.Button(body, "btn_green", Loc.Tl("저장"), 32, () => Save(input.text)).gameObject, -1, 96);
        }

        async void Save(string raw)
        {
            if (m_busy) return;
            string v = (raw ?? "").Trim();
            if (v.Length > 12) v = v.Substring(0, 12);
            if (v.Length > 0 && NickFilter.IsBad(v)) { m_toast.Show(Loc.Tl("사용할 수 없는 닉네임이에요")); return; } // 창은 열어 두고 다시 고치게
            if (v.Length > 0 && v != Profile.Nick)
            {
                m_busy = true;
                try { await Profile.Save(v); }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Profile] SetNick 실패: " + ex.Message);
                    m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
                    return;
                }
                finally { m_busy = false; }
            }
            m_panel.Close();
            m_closed?.Invoke();
        }
    }
}
