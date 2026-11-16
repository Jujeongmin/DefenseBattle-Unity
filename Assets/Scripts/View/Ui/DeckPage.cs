using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 덱 화면 (menu.ts openUnits · unitDeckHtml · attackDeckHtml · openDeckPresets):
    /// 유닛 탭 = 덱 4칸 + 유닛 카드(레벨·카드 막대·스킬·능력치·넣기/빼기·강화·해금),
    /// 몹 카드 탭 = 덱 4칸 + 몹 카드(넣기 → 칸 선택), 추천 덱.
    /// 편성은 사본에 적용하고 창을 닫을 때 SetDecks 로 서버에 보낸다. 강화·해금은 바로 서버.
    /// 원본의 끌어다 놓기 대신 원본에도 있는 버튼 방식(넣기/빼기·칸 선택)만 쓴다.
    /// </summary>
    public sealed class DeckPage
    {
        readonly SpriteCatalog m_cat;
        readonly Modal m_panel;
        readonly Modal m_modal;
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly Action m_closed;

        bool m_attackTab;
        AttackKey? m_pick;
        List<Job> m_deck;
        List<AttackKey> m_attack;
        Action m_after;
        bool m_busy;

        public DeckPage(SpriteCatalog cat, Modal panel, Modal modal, Toast toast, BattleAudio audio, Action closed)
        {
            m_cat = cat;
            m_panel = panel;
            m_modal = modal;
            m_toast = toast;
            m_audio = audio;
            m_closed = closed;
        }

        /// <summary>덱 화면 열기 (after = 닫고 저장한 뒤 할 일, 예: 스테이지 정보로 돌아가기)</summary>
        public void Open(Action after = null)
        {
            EconState e = EconCache.State;
            m_deck = new List<Job>(e.Deck);
            m_attack = new List<AttackKey>(e.AttackDeck);
            m_pick = null;
            m_after = after;
            Draw();
        }

        void Draw()
        {
            EconState e = EconCache.State;
            RectTransform body = m_panel.Panel(Loc.Tl("덱") + "  " + Loc.Icon("gem") + Ui.Num(e.Crystals), Close);

            RectTransform tabs = Ui.Rect(body, "Tabs");
            Ui.Size(tabs.gameObject, -1, 76);
            HorizontalLayoutGroup tg = Ui.Row(tabs, 10);
            tg.childForceExpandWidth = true;
            Ui.Size(Ui.Button(tabs, m_attackTab ? "btn_gray" : "btn_blue", Loc.Icon("helmet") + " " + Loc.Tl("유닛"), 28, () => SetTab(false)).gameObject, -1, 76, 1);
            Ui.Size(Ui.Button(tabs, m_attackTab ? "btn_blue" : "btn_gray", Loc.Icon("skull") + " " + Loc.Tl("몹 카드"), 28, () => SetTab(true)).gameObject, -1, 76, 1);

            if (m_attackTab) AttackTab(body, e);
            else UnitTab(body, e);
        }

        void SetTab(bool attack)
        {
            m_attackTab = attack;
            m_pick = null;
            Draw();
        }

        /// <summary>닫기: 편성이 바뀌었으면 서버에 저장 (거절되면 서버 값으로 되돌아감)</summary>
        async void Close()
        {
            EconState e = EconCache.State;
            bool changed = !Same(m_deck, e.Deck) || !Same(m_attack, e.AttackDeck);
            if (changed)
            {
                var args = new Dictionary<string, object> { { "deck", m_deck.ToArray() }, { "attackDeck", m_attack.ToArray() } };
                try
                {
                    bool ok = await CloudClient.Econ<bool>(CloudApi.SetDecks, args);
                    if (!ok) m_toast.Show(Loc.Tl("덱에 최소 1종은 있어야 해요"));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Deck] SetDecks 실패: " + ex.Message);
                    m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
                }
            }
            m_closed?.Invoke();
            Action after = m_after;
            m_after = null;
            after?.Invoke();
        }

        static bool Same<T>(List<T> a, List<T> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
            return true;
        }

        // ======================= 유닛 탭 =======================
        void UnitTab(Transform body, EconState e)
        {
            int gap = DeckRules.Gap(e, m_deck);
            if (gap > 0)
                Ui.Text(body, Loc.Icon("warning") + " " + Loc.Tl("덱에 빈 칸이 {0}개 있어요 · 소환 비용 {1}{2} (4칸을 채우면 {1}{3})",
                    gap, Loc.Icon("coin"), DeckRules.GapSummonCost(gap), Economy.SummonCost), 22, Ui.Red, TextAlignmentOptions.Center, true);
            RecsRow(body, false);

            RectTransform slots = SlotRow(body);
            for (int i = 0; i < Units.DeckSize; i++)
            {
                if (i < m_deck.Count)
                {
                    Job j = m_deck[i];
                    Button s = Slot(slots, "wood_tight", () =>
                    {
                        if (Progress.RemoveDeck(m_deck, j)) m_audio.Play(Sfx.Button);
                        else m_toast.Show(Loc.Tl("덱에 최소 1종은 있어야 해요"));
                        Draw();
                    });
                    Sprite(s.transform, m_cat.Unit(j, 0)[1], 76);
                    Ui.Line(s.transform, Loc.Tl(Units.Get(j).Kr), 20, Ui.Cream);
                }
                else
                {
                    Button s = Slot(slots, "paper_dark", null);
                    s.interactable = false;
                    Ui.Icon(s.transform, "plus", 40, Ui.Cream2);
                }
            }

            RectTransform grid = Grid(body, 300);
            foreach (UnitDef u in DeckRules.SortedUnits(e)) UnitCard(grid, e, u);
        }

        void UnitCard(Transform grid, EconState e, UnitDef u)
        {
            Job job = u.Job;
            int lv = e.LevelOf(job);
            bool owned = lv > 0;
            bool inDeck = m_deck.Contains(job);
            bool maxed = lv >= Units.MaxLevel;
            int need = owned && !maxed ? Units.CardsNeeded(u.Rarity, lv) : 0;
            int have = e.CardCount(job);
            bool ready = owned && !maxed && have >= need;

            Image card = Ui.Frame(grid, inDeck ? "btn_green" : "paper_dark", 1.8f, u.Id); // 덱에 든 카드 = 초록 (크림 글자가 읽히게)
            Ui.Column(card.transform, 4, 14, 14, 12, 16);
            if (!owned) card.color = new Color(0.75f, 0.75f, 0.75f, 1);

            // 희귀도 + 머리 (그림·이름·레벨)
            Ui.Line(card.transform, Loc.Tl(Units.RarityKr[(int)u.Rarity]), 18, Ui.Hex(Units.RarityColor[(int)u.Rarity]), TextAlignmentOptions.Left);
            RectTransform head = Ui.Rect(card.transform, "Head");
            Ui.Size(head.gameObject, -1, 72);
            Ui.Row(head, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Image pic = Sprite(head, m_cat.Unit(job, 0)[1], 68);
            Ui.Size(pic.gameObject, 68, 68);
            if (!owned) pic.color = new Color(0.4f, 0.4f, 0.4f, 1);
            RectTransform nm = Ui.Rect(head, "Name");
            Ui.Size(nm.gameObject, -1, -1, 1);
            Ui.Column(nm, 0, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(nm, Loc.Tl(u.Kr), 26, Ui.Cream, TextAlignmentOptions.Left);
            Ui.Line(nm, owned ? "Lv." + lv : Loc.Icon("lock") + " " + UnlockRoute(u), 20, Ui.Cream2, TextAlignmentOptions.Left);

            // 카드 막대
            Image bar = Ui.Image(card.transform, null, new Color32(0, 0, 0, 120), "CardBar");
            Ui.Size(bar.gameObject, -1, 30);
            if (owned)
            {
                Image fill = Ui.Image(bar.transform, null, ready ? Ui.Green : Ui.Blue, "Fill");
                RectTransform fr = fill.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = new Vector2(maxed ? 1 : Mathf.Clamp01(have / (float)need), 1);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
                Ui.Fill(Ui.Line(bar.transform, maxed ? "MAX" : Loc.Icon("ticket") + have + "/" + need, 20, Ui.Cream).rectTransform);
            }
            else if (!u.UnlockCrystals.HasValue && TrophyRoad.UnitAt(job) == null && u.UnlockStage == null)
                Ui.Fill(Ui.Line(bar.transform, Loc.Tl("상자·뽑기에서"), 18, Ui.Cream2).rectTransform);

            Ui.Line(card.transform, Loc.Tl(Skills.Kr[(int)job]) + (u.Role != Skills.Kr[(int)job] ? " · " + Loc.Tl(u.Role) : ""), 20, Ui.Gold, TextAlignmentOptions.Left);
            Ui.Line(card.transform, Loc.Tl("공격력") + " " + DeckRules.UnitAttack(job, Math.Max(1, lv))
                + "  " + Loc.Tl("공속") + " " + (1 / u.Cooldown).ToString("0.0") + Loc.Tl("/초")
                + "  " + Loc.Tl("사거리") + " " + Mathf.RoundToInt((float)(u.RangeMult * 100)) + "%", 18, Ui.Cream2, TextAlignmentOptions.Left);

            RectTransform acts = Ui.Rect(card.transform, "Acts");
            Ui.Size(acts.gameObject, -1, 64);
            HorizontalLayoutGroup ag = Ui.Row(acts, 8);
            ag.childForceExpandWidth = true;
            if (!owned)
            {
                if (u.UnlockCrystals.HasValue)
                    Ui.Size(Ui.Button(acts, "btn_gold", Loc.Icon("gem") + u.UnlockCrystals.Value + " " + Loc.Tl("해금"), 22, () => Buy(u)).gameObject, -1, 64, 1);
                return;
            }
            Ui.Size(Ui.Button(acts, inDeck ? "btn_red" : "btn_blue", inDeck ? Loc.Tl("빼기") : Loc.Tl("넣기"), 22, () =>
            {
                if (!DeckRules.Toggle(EconCache.State, m_deck, job))
                    m_toast.Show(m_deck.Count >= Units.DeckSize && !m_deck.Contains(job) ? Loc.Tl("덱이 가득 찼어요. 덱 칸에 끌어 놓아 바꾸세요") : Loc.Tl("덱에 최소 1종은 있어야 해요"));
                Draw();
            }).gameObject, -1, 64, 1);
            Button up = Ui.Button(acts, ready ? "btn_green" : "btn_gray", maxed ? "MAX" : Loc.Tl("강화") + " " + Loc.Icon("gem") + Units.UpgradeCost(lv), 22, () => Upgrade(job));
            up.interactable = !maxed;
            Ui.Size(up.gameObject, -1, 64, 1);
        }

        /// <summary>잠긴 유닛이 열리는 곳: 트로피 로드 / 스테이지</summary>
        static string UnlockRoute(UnitDef u)
        {
            int? at = TrophyRoad.UnitAt(u.Job);
            string s = at.HasValue ? Loc.Icon("trophy") + at.Value : "";
            if (u.UnlockStage != null) s += (s.Length > 0 ? " / " : "") + u.UnlockStage;
            return s;
        }

        async void Upgrade(Job job)
        {
            if (m_busy) return;
            EconState e = EconCache.State;
            int lv = e.LevelOf(job);
            int need = Units.CardsNeeded(Units.Get(job).Rarity, lv);
            if (e.CardCount(job) < need) { m_toast.Show(Loc.Tl("카드 {0}장이 더 필요해요 · 상자와 뽑기에서 얻어요", need - e.CardCount(job))); return; }
            if (e.Crystals < Units.UpgradeCost(lv)) { m_toast.Show(Loc.Tl("보석이 부족해요")); return; }
            if (await Server(CloudApi.UpgradeUnit, CloudClient.Args("job", job))) m_audio.Play(Sfx.Merge);
            Draw();
        }

        void Buy(UnitDef u)
        {
            if (m_busy) return;
            int cost = u.UnlockCrystals ?? 0;
            if (EconCache.State.Crystals < cost) { m_toast.Show(Loc.Tl("보석이 부족해요")); return; }
            m_modal.Confirm(Loc.Tl("유닛 해금"), Loc.Tl("{0}을(를) {1}{2}로 해금할까요?", Loc.Tl(u.Kr), Loc.Icon("gem"), cost), Loc.Tl("해금"), async ok =>
            {
                if (!ok) return;
                if (await Server(CloudApi.BuyUnit, CloudClient.Args("job", u.Job)))
                {
                    m_audio.Play(Sfx.Unlock);
                    m_toast.Show(Loc.Tl("{0} 해금!", Loc.Tl(u.Kr)));
                }
                Draw();
            }, "btn_gold");
        }

        // ======================= 몹 카드 탭 =======================
        void AttackTab(Transform body, EconState e)
        {
            if (m_pick != null) Ui.Text(body, Loc.Tl("{0} 넣을 칸을 누르세요", Loc.Icon("pointer")), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            RecsRow(body, true);

            RectTransform slots = SlotRow(body);
            for (int i = 0; i < m_attack.Count; i++)
            {
                int slot = i;
                AttackDef a = Attacks.Get(m_attack[i]);
                Button s = Slot(slots, m_pick != null ? "btn_gold" : "wood_tight", () =>
                {
                    if (m_pick == null) { m_toast.Show(Loc.Tl("몹 카드는 4장 모두 들고 가요. 다른 카드를 칸에 놓아 바꾸세요")); return; }
                    if (Progress.PlaceAttack(EconCache.State, m_attack, m_pick.Value, slot)) m_audio.Play(Sfx.Button);
                    m_pick = null;
                    Draw();
                });
                Sprite(s.transform, m_cat.Mob(a.Race, a.Variant)[1], 76);
                Ui.Line(s.transform, Loc.Tl(a.Kr), 20, Ui.Cream);
            }

            RectTransform grid = Grid(body, 330);
            foreach (AttackDef a in DeckRules.SortedAttacks(e)) AttackCard(grid, e, a);
        }

        void AttackCard(Transform grid, EconState e, AttackDef a)
        {
            bool open = EconRules.AttackUnlocked(e, a.Key);
            bool inDeck = m_attack.Contains(a.Key);
            bool picked = m_pick == a.Key;
            Image card = Ui.Frame(grid, picked ? "btn_gold" : inDeck ? "btn_blue" : "paper_dark", 1.8f, a.Id);
            Ui.Column(card.transform, 4, 14, 14, 12, 16);
            if (!open) card.color = new Color(0.75f, 0.75f, 0.75f, 1);

            RectTransform head = Ui.Rect(card.transform, "Head");
            Ui.Size(head.gameObject, -1, 72);
            Ui.Row(head, 8, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Image pic = Sprite(head, m_cat.Mob(a.Race, a.Variant)[1], 68);
            Ui.Size(pic.gameObject, 68, 68);
            if (!open) pic.color = new Color(0.4f, 0.4f, 0.4f, 1);
            RectTransform nm = Ui.Rect(head, "Name");
            Ui.Size(nm.gameObject, -1, -1, 1);
            Ui.Column(nm, 0, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(nm, Loc.Tl(a.Kr), 26, Ui.Cream, TextAlignmentOptions.Left);
            string cost = Loc.Icon("coin") + a.Cost + (a.Cooldown > 0 ? " · " + Loc.Icon("timer") + Mathf.RoundToInt((float)(a.Cooldown / Waves.PlaySpeed)) + "s" : "");
            Ui.Line(nm, cost, 20, Ui.Cream2, TextAlignmentOptions.Left);

            TextMeshProUGUI desc = Ui.Text(card.transform, Loc.Tl(a.Desc), 18, Ui.Cream2, TextAlignmentOptions.TopLeft);
            Ui.Size(desc.gameObject, -1, 48);
            Ui.Line(card.transform, Loc.Tl("마리") + " " + a.Count + "  " + Loc.Tl("체력") + " " + Mathf.RoundToInt((float)(a.HpMult * 100)) + "%  "
                + Loc.Tl("속도") + " " + Mathf.RoundToInt((float)(a.Speed * 100)) + "%", 18, Ui.Cream2, TextAlignmentOptions.Left);

            RectTransform ctr = Ui.Rect(card.transform, "Counters");
            Ui.Size(ctr.gameObject, -1, 44);
            Ui.Row(ctr, 4, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Ui.Line(ctr, Loc.Tl("막는 유닛"), 18, Ui.Gold, TextAlignmentOptions.Left);
            foreach (Job j in a.Counters) Ui.Size(Sprite(ctr, m_cat.Unit(j, 0)[1], 40).gameObject, 40, 40);

            RectTransform acts = Ui.Rect(card.transform, "Acts");
            Ui.Size(acts.gameObject, -1, 64);
            HorizontalLayoutGroup ag = Ui.Row(acts, 8);
            ag.childForceExpandWidth = true;
            if (!open)
                Ui.Line(acts, Loc.Icon("lock") + " " + Loc.Icon("trophy") + TrophyRoad.AttackAt(a.Key), 22, Ui.Cream2);
            else if (inDeck)
                Ui.Line(acts, Loc.Icon("check") + " " + Loc.Tl("덱"), 24, Ui.Cream);
            else
                Ui.Size(Ui.Button(acts, picked ? "btn_gold" : "btn_blue", picked ? Loc.Tl("칸 선택") : Loc.Tl("넣기"), 22, () =>
                {
                    m_pick = picked ? (AttackKey?)null : a.Key;
                    Draw();
                }).gameObject, -1, 64, 1);
        }

        // ======================= 추천 덱 =======================
        void RecsRow(Transform body, bool attack)
        {
            RectTransform row = Ui.Rect(body, "Recs");
            Ui.Size(row.gameObject, -1, 60);
            Ui.Row(row, 0, 0, 0, 0, 0, TextAnchor.MiddleRight);
            Ui.Size(Ui.Button(row, "btn_gold", Loc.Icon("star") + " " + Loc.Tl("추천 덱"), 22, () => OpenPresets(attack)).gameObject, 220, 60);
        }

        void OpenPresets(bool attack)
        {
            EconState e = EconCache.State;
            RectTransform body = m_modal.Custom(Loc.Tl("추천") + " " + (attack ? Loc.Tl("몹 카드") : Loc.Tl("유닛")) + " " + Loc.Tl("덱"));
            int n = attack ? Attacks.AttackPresets.Length : Attacks.UnitPresets.Length;
            for (int i = 0; i < n; i++)
            {
                string name, desc;
                int missing, count;
                bool same;
                if (attack)
                {
                    DeckPreset<AttackKey> p = Attacks.AttackPresets[i];
                    name = p.Name; desc = p.Desc; missing = DeckRules.Missing(e, p); count = p.Cards.Length;
                    same = SameSet(m_attack, p.Cards);
                }
                else
                {
                    DeckPreset<Job> p = Attacks.UnitPresets[i];
                    name = p.Name; desc = p.Desc; missing = DeckRules.Missing(e, p); count = p.Cards.Length;
                    same = SameSet(m_deck, p.Cards);
                }
                Image box = Ui.Frame(body, "paper_dark", 1.8f, "Preset");
                Ui.Column(box.transform, 4, 16, 16, 10, 14);
                RectTransform head = Ui.Rect(box.transform, "Head");
                Ui.Size(head.gameObject, -1, 56);
                Ui.Row(head, 8);
                TextMeshProUGUI t = Ui.Line(head, Loc.Tl(name), 26, Ui.Gold, TextAlignmentOptions.Left);
                Ui.Size(t.gameObject, -1, -1, 1);
                int idx = i;
                Button b;
                if (same) b = Ui.Button(head, "btn_gray", Loc.Icon("check") + " " + Loc.Tl("사용 중"), 20, null);
                else if (missing > 0) b = Ui.Button(head, "btn_gray", Loc.Icon("lock") + " " + Loc.Tl("{0}장 없음", missing), 20, null);
                else b = Ui.Button(head, "btn_gold", Loc.Tl("장착"), 22, () => UsePreset(attack, idx));
                b.interactable = !same && missing == 0;
                Ui.Size(b.gameObject, 170, 56);
                Ui.Text(box.transform, Loc.Tl(desc), 18, Ui.Cream2, TextAlignmentOptions.Left);
                RectTransform cards = Ui.Rect(box.transform, "Cards");
                Ui.Size(cards.gameObject, -1, 56);
                Ui.Row(cards, 6, 0, 0, 0, 0, TextAnchor.MiddleLeft);
                for (int c = 0; c < count; c++)
                {
                    Image pic;
                    bool has;
                    if (attack)
                    {
                        AttackDef a = Attacks.Get(Attacks.AttackPresets[i].Cards[c]);
                        pic = Sprite(cards, m_cat.Mob(a.Race, a.Variant)[1], 52);
                        has = EconRules.AttackUnlocked(e, a.Key);
                    }
                    else
                    {
                        Job j = Attacks.UnitPresets[i].Cards[c];
                        pic = Sprite(cards, m_cat.Unit(j, 0)[1], 52);
                        has = e.Owns(j);
                    }
                    Ui.Size(pic.gameObject, 52, 52);
                    if (!has) pic.color = new Color(0.35f, 0.35f, 0.35f, 1);
                }
            }
        }

        void UsePreset(bool attack, int i)
        {
            EconState e = EconCache.State;
            if (attack)
            {
                DeckPreset<AttackKey> p = Attacks.AttackPresets[i];
                if (DeckRules.Missing(e, p) > 0) return;
                m_attack = new List<AttackKey>(p.Cards);
                m_toast.Show(Loc.Tl("{0} 덱 장착", Loc.Tl(p.Name)));
            }
            else
            {
                DeckPreset<Job> p = Attacks.UnitPresets[i];
                if (DeckRules.Missing(e, p) > 0) return;
                m_deck = new List<Job>(p.Cards);
                m_toast.Show(Loc.Tl("{0} 덱 장착", Loc.Tl(p.Name)));
            }
            m_audio.Play(Sfx.Merge);
            m_modal.Close();
            Draw();
        }

        static bool SameSet<T>(List<T> cur, T[] cards)
        {
            if (cur.Count != cards.Length) return false;
            foreach (T c in cards) if (!cur.Contains(c)) return false;
            return true;
        }

        // ======================= 공통 =======================
        RectTransform SlotRow(Transform body)
        {
            RectTransform row = Ui.Rect(body, "Slots");
            Ui.Size(row.gameObject, -1, 140);
            HorizontalLayoutGroup g = Ui.Row(row, 10);
            g.childForceExpandWidth = true;
            return row;
        }

        Button Slot(Transform row, string frame, Action click)
        {
            Button b = Ui.Button(row, frame, null, 0, click, 1.8f);
            Ui.Size(b.gameObject, -1, 140, 1);
            Ui.Column(b.transform, 0, 4, 4, 8, 12, TextAnchor.MiddleCenter);
            return b;
        }

        static RectTransform Grid(Transform body, float cellH)
        {
            RectTransform grid = Ui.Rect(body, "Grid");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(306, cellH);
            g.spacing = new Vector2(12, 12);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;
            return grid;
        }

        static Image Sprite(Transform parent, Sprite s, float size)
        {
            Image img = Ui.Image(parent, s, Color.white, "Pic");
            img.preserveAspect = true;
            Ui.Size(img.gameObject, -1, size);
            return img;
        }

        /// <summary>서버 경제 함수 (두 번 누름 막음)</summary>
        async System.Threading.Tasks.Task<bool> Server(string fn, Dictionary<string, object> args)
        {
            m_busy = true;
            try { return await CloudClient.Econ<bool>(fn, args); }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deck] " + fn + " 실패: " + ex.Message);
                m_toast.Show(Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요."));
                return false;
            }
            finally { m_busy = false; }
        }
    }
}
