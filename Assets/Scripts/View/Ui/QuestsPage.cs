using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>퀘스트 (menu.ts openQuests): 일일 퀘스트(진행은 기기, 받기는 서버 ClaimQuest) + 업적 목록</summary>
    public sealed class QuestsPage
    {
        readonly Modal m_panel;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly Action m_closed;
        bool m_busy;

        public QuestsPage(Modal panel, Toast toast, BattleAudio audio, Action closed)
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
            string today = LocalMeta.Today;
            RectTransform body = m_panel.Panel(Loc.Tl("퀘스트"), m_closed);
            Head(body, Loc.Tl("일일 퀘스트"), Loc.Tl("매일 0시(UTC) 초기화"));
            foreach (DailyQuestDef q in Quests.Daily)
            {
                LocalMeta.Data.Daily.Progress.TryGetValue(q.Key, out int v);
                int prog = Math.Min(q.Goal, v);
                bool done = prog >= q.Goal;
                bool claimed = MetaRules.QuestClaimed(e, q.Key, today);
                Image row = Ui.Frame(body, "paper_dark", 1.8f, q.Key);
                Ui.Size(row.gameObject, -1, 130);
                Ui.Row(row.transform, 12, 20, 16, 12, 14);
                RectTransform info = Ui.Rect(row.transform, "Info");
                Ui.Size(info.gameObject, -1, -1, 1);
                Ui.Column(info, 6, 0, 0, 0, 0, TextAnchor.MiddleLeft);
                Ui.Line(info, Loc.Tl(q.Kr), 26, Ui.Cream, TextAlignmentOptions.Left);
                Image bar = Ui.Image(info, null, new Color(0, 0, 0, 0.45f), "Bar");
                Ui.Size(bar.gameObject, -1, 16);
                Image fill = Ui.Image(bar.transform, null, Ui.Gold, "Fill");
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(prog / (float)q.Goal, 1);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                Ui.Line(info, prog + "/" + q.Goal, 20, Ui.Cream2, TextAlignmentOptions.Left);
                string key = q.Key;
                Button b = Ui.Button(row.transform, done && !claimed ? "btn_gold" : "btn_gray",
                    claimed ? Loc.Tl("완료") : Loc.Icon("gem") + " " + q.Reward, 24, () => Claim(key));
                b.interactable = done && !claimed;
                Ui.Size(b.gameObject, 160, 76);
            }
            Head(body, Loc.Tl("업적"), null);
            foreach (AchievementDef a in Quests.Achievements)
            {
                bool got = e.Achievements.Contains(a.Key);
                Image row = Ui.Frame(body, "paper_dark", 1.8f, a.Key);
                Ui.Size(row.gameObject, -1, 100);
                Ui.Row(row.transform, 12, 20, 20, 10, 12);
                RectTransform info = Ui.Rect(row.transform, "Info");
                Ui.Size(info.gameObject, -1, -1, 1);
                Ui.Column(info, 2, 0, 0, 0, 0, TextAnchor.MiddleLeft);
                Ui.Line(info, Loc.Tl(a.Kr), 24, Ui.Cream, TextAlignmentOptions.Left);
                Ui.Line(info, Loc.Tl("보상") + " " + Loc.Icon("gem") + " " + a.Reward, 20, Ui.Cream2, TextAlignmentOptions.Left);
                Ui.Size(Ui.Line(row.transform, got ? Loc.Tl("달성 {0}", Loc.Icon("check")) : Loc.Tl("미달성"), 22, got ? Ui.Gold : Ui.Cream2).gameObject, 150, -1);
            }
        }

        static void Head(Transform body, string title, string note)
        {
            RectTransform head = Ui.Rect(body, "Head");
            Ui.Size(head.gameObject, -1, 52);
            Ui.Row(head, 10, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(head, title, 30, Ui.Ink, TextAlignmentOptions.Left, true);
            if (note != null) Ui.Size(Ui.Line(head, note, 20, Ui.Ink2, TextAlignmentOptions.Left, true).gameObject, -1, -1, 1);
        }

        async void Claim(string key)
        {
            if (m_busy) return;
            m_busy = true;
            try
            {
                if (await CloudClient.Econ<int>(CloudApi.ClaimQuest, CloudClient.Args("key", key)) > 0) m_audio.Play(Sfx.Coin);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Quests] ClaimQuest 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
            }
            finally { m_busy = false; }
            Draw();
        }
    }
}
