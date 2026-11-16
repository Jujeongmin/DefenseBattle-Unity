using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>연구소 (menu.ts openResearch): 연구 5종 - 레벨·설명·칸 표시·보석으로 올리기 (서버 Research)</summary>
    public sealed class ResearchPage
    {
        readonly Modal m_panel;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly Action m_closed;
        bool m_busy;

        public ResearchPage(Modal panel, Toast toast, BattleAudio audio, Action closed)
        {
            m_panel = panel;
            m_toast = toast;
            m_audio = audio;
            m_closed = closed;
        }

        public void Open() => Draw();

        void Draw()
        {
            EconState e = EconCache.State;
            RectTransform body = m_panel.Panel(Loc.Tl("연구소") + "  " + Loc.Icon("gem") + Ui.Num(e.Crystals), m_closed);
            Ui.Text(body, Loc.Tl("보석으로 올리면 대전·캠페인·무한 모드 모든 판에 계속 적용돼요"), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            foreach (ResearchDef r in Core.Research.All) Row(body, e, r);
        }

        void Row(Transform body, EconState e, ResearchDef r)
        {
            int lv = e.ResearchLevel(r.Key);
            bool maxed = lv >= r.Max;
            int cost = Core.Research.Cost(lv);
            Image row = Ui.Frame(body, "paper_dark", 1.8f, r.Id);
            Ui.Size(row.gameObject, -1, 150);
            Ui.Row(row.transform, 12, 20, 16, 12, 14);
            RectTransform info = Ui.Rect(row.transform, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(info, Loc.Tl(r.Kr) + "  <color=#ffc928>Lv." + lv + "/" + r.Max + "</color>", 28, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(info, Loc.Tl(r.Desc), 20, Ui.Cream2, TextAlignmentOptions.Left);
            RectTransform pips = Ui.Rect(info, "Pips");
            Ui.Size(pips.gameObject, -1, 18);
            HorizontalLayoutGroup pg = Ui.Row(pips, 3, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            pg.childForceExpandWidth = true;
            for (int i = 0; i < r.Max; i++)
                Ui.Size(Ui.Image(pips, null, i < lv ? (Color)Ui.Gold : new Color(0, 0, 0, 0.45f), "Pip").gameObject, -1, 18, 1);
            Button b = Ui.Button(row.transform, maxed ? "btn_gray" : "btn_green", maxed ? "MAX" : Loc.Icon("gem") + " " + cost, 26, () => Up(r));
            b.interactable = !maxed;
            Ui.Size(b.gameObject, 170, 84);
        }

        async void Up(ResearchDef r)
        {
            if (m_busy) return;
            EconState e = EconCache.State;
            int lv = e.ResearchLevel(r.Key);
            if (lv >= r.Max) return;
            if (e.Crystals < Core.Research.Cost(lv)) { m_toast.Show(Loc.Tl("보석이 부족해요")); return; }
            m_busy = true;
            try
            {
                if (await CloudClient.Econ<bool>(CloudApi.Research, CloudClient.Args("key", r.Key))) m_audio.Play(Sfx.Merge);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Research] 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
            }
            finally { m_busy = false; }
            Draw();
        }
    }
}
