using System;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 캠페인 창들 (menu.ts openStages / openStageInfo / openEndless):
    /// 챕터 탭 + 스테이지 10칸 + 챕터 별 보상, 스테이지 정보(별·웨이브·종족·보상·해금 유닛·출전 덱·골드 부스터), 무한 모드 정보.
    /// </summary>
    public sealed class CampaignPages
    {
        readonly SpriteCatalog m_cat;
        readonly Modal m_panel;
        readonly Modal m_modal;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly Action<BattleLaunch> m_launch;
        readonly Action m_closed;
        /// <summary>덱 편집 열기 (닫히면 넘겨 준 동작으로 돌아온다)</summary>
        public Action<Action> OpenDeck;
        int m_chapter = 1;
        bool m_busy;

        public CampaignPages(SpriteCatalog cat, Modal panel, Modal modal, Toast toast, BattleAudio audio, Action<BattleLaunch> launch, Action closed)
        {
            m_cat = cat;
            m_panel = panel;
            m_modal = modal;
            m_toast = toast;
            m_audio = audio;
            m_launch = launch;
            m_closed = closed;
        }

        // ======================= 스테이지 선택 =======================
        public void OpenStages()
        {
            m_chapter = Campaign.Get(Progress.NextStageId(EconCache.State)).Chapter;
            DrawStages();
        }

        void DrawStages()
        {
            EconState e = EconCache.State;
            int c = m_chapter;
            RectTransform body = m_panel.Panel(Loc.Tl("스테이지"), m_closed);

            RectTransform tabs = Ui.Rect(body, "Chapters");
            Ui.Size(tabs.gameObject, -1, 76);
            HorizontalLayoutGroup tg = Ui.Row(tabs, 8);
            tg.childForceExpandWidth = true;
            foreach (ChapterDef ch in Campaign.Chapters)
            {
                int n = ch.N;
                string label = (Progress.ChapterUnlocked(e, n) ? "" : Loc.Icon("lock") + " ") + n + ". " + Loc.Tl(ch.Kr);
                Button b = Ui.Button(tabs, n == c ? "btn_blue" : "btn_gray", label, 24, () => { m_chapter = n; DrawStages(); });
                Ui.Size(b.gameObject, -1, 76, 1);
            }
            ChapterDef cd = Campaign.Chapters[c - 1];
            Ui.Text(body, Loc.Tl(cd.Desc) + " " + Loc.Tl("· 스테이지당 {0}웨이브", cd.Waves), 22, Ui.Ink2, TextAlignmentOptions.Center, true);

            RectTransform grid = Ui.Rect(body, "Stages");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(118, 128);
            g.spacing = new Vector2(10, 12);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            g.childAlignment = TextAnchor.UpperCenter;
            Ui.Size(grid.gameObject, -1, 128 * 2 + 12);
            foreach (StageDef s in Campaign.Stages)
            {
                if (s.Chapter != c) continue;
                StageNode(grid, e, s);
            }

            int chStars = Progress.ChapterStars(e, c);
            RectTransform head = Ui.Rect(body, "StarHead");
            Ui.Size(head.gameObject, -1, 44);
            Ui.Row(head, 10);
            TextMeshProUGUI ht = Ui.Line(head, Loc.Tl("챕터 별 보상"), 28, Ui.Ink, TextAlignmentOptions.Left, true);
            Ui.Size(ht.gameObject, -1, -1, 1);
            Ui.Line(head, Loc.Icon("star") + " " + chStars + "/" + Campaign.StagesPerChapter * 3, 26, Ui.Ink, TextAlignmentOptions.Right, true);

            RectTransform goals = Ui.Rect(body, "Goals");
            Ui.Size(goals.gameObject, -1, 84);
            HorizontalLayoutGroup gg = Ui.Row(goals, 10);
            gg.childForceExpandWidth = true;
            for (int i = 0; i < Campaign.ChapterStarGoals.Length; i++)
            {
                int goal = i;
                bool claimed = Progress.StarRewardClaimed(e, c, i);
                bool ready = chStars >= Campaign.ChapterStarGoals[i] && !claimed;
                string label = Loc.Icon("star") + Campaign.ChapterStarGoals[i] + " " + (claimed ? Loc.Icon("check") : Loc.Icon("gem") + Campaign.ChapterStarReward(i));
                Button b = Ui.Button(goals, ready ? "btn_gold" : "btn_gray", label, 26, () => ClaimStar(c, goal));
                b.interactable = ready;
                Ui.Size(b.gameObject, -1, 84, 1);
            }
        }

        void StageNode(Transform grid, EconState e, StageDef s)
        {
            int stars = e.StarsOf(s.Id);
            bool open = Progress.StageUnlocked(e, s.Id);
            bool isNext = open && stars == 0;
            string frame = !open ? "btn_gray" : isNext ? "btn_red" : s.Id.EndsWith("-10") ? "btn_purple" : "btn_blue";
            string id = s.Id;
            Button b = Ui.Button(grid, frame, null, 0, () =>
            {
                if (!Progress.StageUnlocked(EconCache.State, id)) { m_toast.Show(Loc.Tl("이전 스테이지를 먼저 클리어하세요")); return; }
                OpenStageInfo(id);
            });
            Ui.Column(b.transform, 0, 4, 4, 8, 16, TextAnchor.MiddleCenter);
            Ui.Line(b.transform, s.Id, 30, Ui.Cream);
            Ui.Line(b.transform, open ? StarText(stars) : Loc.Icon("lock"), 22, Ui.Cream);
            if (s.Unlocks != null && !e.Owns(s.Unlocks.Value))
            {
                Image u = Ui.Image(b.transform, m_cat.Unit(s.Unlocks.Value, 0)[1], Color.white, "Unlock");
                u.preserveAspect = true;
                u.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Ui.Place(u.rectTransform, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(46, 46));
            }
        }

        static string StarText(int stars)
        {
            string on = Loc.Icon("star"), off = Loc.Icon("star_off");
            return (stars > 0 ? on : off) + (stars > 1 ? on : off) + (stars > 2 ? on : off);
        }

        async void ClaimStar(int chapter, int goal)
        {
            if (m_busy) return;
            m_busy = true;
            try
            {
                int n = await CloudClient.Econ<int>(CloudApi.ClaimStarReward, CloudClient.Args("chapter", chapter, "goal", goal));
                if (n > 0)
                {
                    m_audio.Play(Sfx.Coin);
                    m_toast.Show(Loc.Tl("별 보상 {0} +{1}", Loc.Icon("gem"), n));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Campaign] ClaimStarReward 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
            }
            finally { m_busy = false; }
            DrawStages();
        }

        // ======================= 스테이지 정보 =======================
        public void OpenStageInfo(string id) => DrawStageInfo(id, false);

        void DrawStageInfo(string id, bool booster)
        {
            EconState e = EconCache.State;
            StageDef st = Campaign.Get(id);
            int stars = e.StarsOf(id);
            RectTransform body = m_modal.Custom(Loc.Tl("스테이지") + " " + id);

            Ui.Line(body, StarText(stars), 40, Ui.Gold);

            RectTransform facts = Ui.Rect(body, "Facts");
            Ui.Size(facts.gameObject, -1, 56);
            Ui.Row(facts, 18);
            Ui.Line(facts, Loc.Icon("flag") + " " + st.Waves, 28, Ui.Ink, TextAlignmentOptions.Center, true);
            Ui.Line(facts, Loc.Icon("skull") + " " + Loc.Tl("보스"), 28, Ui.Ink, TextAlignmentOptions.Center, true);
            Race[] races = st.Races ?? new[] { Race.Troll, Race.Orc, Race.Undead };
            foreach (Race r in races)
            {
                Image ri = Ui.Image(facts, m_cat.Mob(r, 0)[1], Color.white, r.ToString());
                ri.preserveAspect = true;
                Ui.Size(ri.gameObject, 52, 52);
            }
            Ui.Line(facts, Loc.Icon("gem") + " " + (stars > 0 ? st.ReplayReward : st.FirstReward), 28, Ui.Ink, TextAlignmentOptions.Center, true);

            if (st.Unlocks != null && !e.Owns(st.Unlocks.Value))
            {
                Image row = Ui.Frame(body, "btn_gold", 1.8f, "Unlock");
                Ui.Size(row.gameObject, -1, 76);
                Ui.Row(row.transform, 12, 16, 16, 6, 10, TextAnchor.MiddleLeft);
                Image u = Ui.Image(row.transform, m_cat.Unit(st.Unlocks.Value, 0)[1], Color.white, "Unit");
                u.preserveAspect = true;
                Ui.Size(u.gameObject, 56, 56);
                Ui.Line(row.transform, Loc.Tl(Units.Get(st.Unlocks.Value).Kr) + " " + Loc.Icon("lock") + " → NEW", 28, Ui.Cream, TextAlignmentOptions.Left);
            }

            DeckRow(body, e, () => DrawStageInfo(id, booster), true);
            BoosterRow(body, e, booster, Loc.Tl("시작 골드 2배 · 보유 {0}", e.Item(ItemKey.GoldBooster)), v => DrawStageInfo(id, v));
            Button go = Ui.Button(body, "btn_red", Loc.Icon("sword") + " " + Loc.Tl("전투 시작"), 36, () =>
            {
                m_modal.Close();
                m_panel.Close();
                m_launch(new BattleLaunch(BattleLaunch.Mode.Stage, id, booster && EconCache.State.Item(ItemKey.GoldBooster) > 0));
            });
            Ui.Size(go.gameObject, -1, 104);
        }

        // ======================= 무한 모드 정보 =======================
        public void OpenEndless() => DrawEndless(false);

        void DrawEndless(bool booster)
        {
            EconState e = EconCache.State;
            RectTransform body = m_modal.Custom(Loc.Icon("infinity") + " " + Loc.Tl("무한 모드"));
            Ui.Line(body, Loc.Icon("flag") + " " + Loc.Tl("50웨이브 생존 도전"), 30, Ui.Ink, TextAlignmentOptions.Center, true);
            Ui.Text(body, Loc.Tl("최고 기록 {0}웨이브 · 도달 웨이브만큼 보석 획득", LocalMeta.Data.BestWave), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            DeckRow(body, e, () => DrawEndless(booster), false);
            BoosterRow(body, e, booster, Loc.Tl("보유") + " " + e.Item(ItemKey.GoldBooster), DrawEndless);
            Button go = Ui.Button(body, "btn_red", Loc.Icon("sword") + " " + Loc.Tl("시작"), 36, () =>
            {
                m_modal.Close();
                m_launch(new BattleLaunch(BattleLaunch.Mode.Endless, null, booster && EconCache.State.Item(ItemKey.GoldBooster) > 0));
            });
            Ui.Size(go.gameObject, -1, 104);
        }

        // ======================= 공통 =======================
        /// <summary>출전 덱 4칸 (+ 덱 편집 버튼)</summary>
        void DeckRow(Transform body, EconState e, Action reopen, bool withEdit)
        {
            if (withEdit)
            {
                RectTransform head = Ui.Rect(body, "DeckHead");
                Ui.Size(head.gameObject, -1, 64);
                Ui.Row(head, 10);
                TextMeshProUGUI t = Ui.Line(head, Loc.Tl("출전 덱"), 28, Ui.Ink, TextAlignmentOptions.Left, true);
                Ui.Size(t.gameObject, -1, -1, 1);
                Button edit = Ui.Button(head, "btn_blue", Loc.Tl("덱 편집"), 24, () =>
                {
                    m_modal.Close();
                    if (OpenDeck != null) OpenDeck(reopen);
                    else m_toast.Show(Loc.Tl("준비 중"));
                });
                Ui.Size(edit.gameObject, 180, 64);
            }
            RectTransform row = Ui.Rect(body, "Deck");
            Ui.Size(row.gameObject, -1, 132);
            HorizontalLayoutGroup g = Ui.Row(row, 10);
            g.childForceExpandWidth = true;
            for (int i = 0; i < Units.DeckSize; i++)
            {
                Image slot = Ui.Frame(row, i < e.Deck.Count ? "wood_tight" : "paper_dark", 1.8f, "Slot");
                Ui.Size(slot.gameObject, -1, 132, 1);
                Ui.Column(slot.transform, 0, 4, 4, 8, 10, TextAnchor.MiddleCenter);
                if (i < e.Deck.Count)
                {
                    Job j = e.Deck[i];
                    Image u = Ui.Image(slot.transform, m_cat.Unit(j, 0)[1], Color.white, j.ToString());
                    u.preserveAspect = true;
                    Ui.Size(u.gameObject, -1, 76);
                    Ui.Line(slot.transform, Loc.Tl(Units.Get(j).Kr), 20, Ui.Cream);
                }
                else Ui.Line(slot.transform, Loc.Tl("<div class=\"deck-slot\">빈 슬롯</div>"), 20, Ui.Cream2);
            }
        }

        /// <summary>골드 부스터 사용 줄 (가진 게 있을 때만)</summary>
        void BoosterRow(Transform body, EconState e, bool on, string desc, Action<bool> redraw)
        {
            if (e.Item(ItemKey.GoldBooster) <= 0) return;
            Button row = Ui.Button(body, on ? "btn_green" : "paper_dark", null, 0, () => redraw(!on), 1.8f);
            Ui.Size(row.gameObject, -1, 88);
            Ui.Row(row.transform, 12, 20, 20, 8, 12, TextAnchor.MiddleLeft);
            Ui.Icon(row.transform, "bag", 44);
            RectTransform info = Ui.Rect(row.transform, "Info");
            Ui.Size(info.gameObject, -1, -1, 1);
            Ui.Column(info, 0, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(info, Loc.Tl("골드 부스터 사용"), 26, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(info, desc, 20, Ui.Cream2, TextAlignmentOptions.Left);
            Ui.Icon(row.transform, on ? "check" : "close", 40);
        }
    }
}
