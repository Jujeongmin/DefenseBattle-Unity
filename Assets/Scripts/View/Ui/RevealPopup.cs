using System;
using System.Collections;
using System.Collections.Generic;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 카드 공개 연출 (chests.ts showReveal): 상자가 떨어져 세 번 흔들리고(영웅 이상이면 마지막에 그 등급 색으로 빛남)
    /// 빛과 함께 터진 뒤, 카드가 낮은 등급부터 한 장씩 나온다. 연출 중 탭하면 끝까지 건너뛴다.
    /// 여러 개가 쌓이면 차례로 (flushReveals). 열 때만 만들고 닫으면 지운다.
    /// </summary>
    public sealed class RevealPopup
    {
        readonly MonoBehaviour m_host;
        readonly Transform m_layer;
        readonly SpriteCatalog m_cat;
        readonly BattleAudio m_audio;
        readonly Queue<Reveal> m_queue = new Queue<Reveal>();
        readonly List<(RectTransform rt, float at)> m_pops = new List<(RectTransform, float)>();
        GameObject m_open;
        Action m_done;
        bool m_skip;

        public RevealPopup(MonoBehaviour host, Transform layer, SpriteCatalog cat, BattleAudio audio)
        {
            m_host = host;
            m_layer = layer;
            m_cat = cat;
            m_audio = audio;
        }

        public bool Busy => m_open != null;

        /// <summary>공개 내용을 쌓고, 떠 있는 게 없으면 바로 보여 준다. onAllDone = 쌓인 것을 다 본 뒤</summary>
        public void Show(Reveal r, Action onAllDone = null)
        {
            if (r == null) { onAllDone?.Invoke(); return; }
            m_queue.Enqueue(r);
            if (onAllDone != null) m_done += onAllDone;
            if (m_open == null) Next();
        }

        /// <summary>쌓인 공개를 다 본 뒤 (지금 없으면 바로)</summary>
        public void WhenDone(Action a)
        {
            if (m_open == null) { a?.Invoke(); return; }
            m_done += a;
        }

        public void Close()
        {
            m_queue.Clear();
            if (m_open != null) UnityEngine.Object.Destroy(m_open);
            m_open = null;
            m_done = null;
        }

        void Next()
        {
            if (m_queue.Count == 0)
            {
                Action d = m_done;
                m_done = null;
                d?.Invoke();
                return;
            }
            m_host.StartCoroutine(Play(m_queue.Dequeue()));
        }

        /// <summary>제목: 서버 제목은 한국어라 종류로 다시 만든다 (revealTitle)</summary>
        static string Title(Reveal r)
        {
            if (r.IsGacha)
            {
                string digits = "";
                foreach (char c in r.Title ?? "") if (char.IsDigit(c)) digits += c;
                return Loc.Tl("유닛 뽑기 {0}회", digits.Length > 0 ? digits : "1");
            }
            if (r.Chest != null) return Loc.Tl(Chests.Get(r.Chest.Value).Kr);
            return r.Title ?? Loc.Tl("상자");
        }

        IEnumerator Play(Reveal r)
        {
            List<CardDrop> order = RevealRules.Order(r.Drops);
            int[] delays = RevealRules.Delays(order);
            Rarity best = RevealRules.Best(order);
            Color32 bestColor = Ui.Hex(Units.RarityColor[(int)best]);
            Color32 boxColor = r.Chest != null ? Ui.Hex(Chests.Get(r.Chest.Value).Color) : bestColor;
            m_skip = false;

            RectTransform root = Ui.Rect(m_layer, "Reveal");
            Ui.Fill(root);
            root.SetAsLastSibling();
            m_open = root.gameObject;
            Button tap = Ui.Hit(root, () => m_skip = true, "Dim");
            tap.GetComponent<Image>().color = new Color32(0, 0, 0, 225);
            Ui.Fill(tap.GetComponent<RectTransform>());

            // ---- 상자 장면 ----
            RectTransform scene = Ui.Rect(root, "Scene");
            Ui.Fill(scene);
            Image glow = Ui.Image(scene, null, new Color(boxColor.r / 255f, boxColor.g / 255f, boxColor.b / 255f, 0.25f), "Glow");
            Ui.Place(glow.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(420, 420));
            glow.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Image box = Ui.Image(scene, r.IsGacha ? m_cat.Icon("summon") : m_cat.Frame("chest"), Color.white, "Box");
            box.preserveAspect = true;
            if (!r.IsGacha) box.color = Color.Lerp(Color.white, boxColor, 0.55f);
            RectTransform br = box.rectTransform;
            Ui.Place(br, new Vector2(0.5f, 0.55f), new Vector2(0, 700), new Vector2(260, 260));
            TextMeshProUGUI skipText = Ui.Line(scene, Loc.Tl("탭해서 건너뛰기"), 24, Ui.Cream2);
            Ui.Place(skipText.rectTransform, new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(600, 40));
            Image flash = Ui.Image(root, null, new Color(1, 1, 1, 0), "Flash");
            Ui.Fill(flash.rectTransform);

            float t0 = Time.unscaledTime;
            int shake = 0;
            bool burst = false;
            while (!m_skip)
            {
                float ms = (Time.unscaledTime - t0) * 1000;
                // 떨어짐 (0~400ms, 살짝 튐)
                float drop = Mathf.Clamp01(ms / 400f);
                float y = Mathf.Lerp(700, 0, 1 - (1 - drop) * (1 - drop));
                float rot = 0;
                if (shake < RevealRules.Shakes.Length && ms >= RevealRules.Shakes[shake])
                {
                    m_audio.Play(Sfx.Button);
                    shake++;
                    if (shake == RevealRules.Shakes.Length && best >= Rarity.Epic) box.color = Color.Lerp(box.color, bestColor, 0.6f); // 좋은 카드 예고
                }
                for (int i = 0; i < RevealRules.Shakes.Length; i++)
                {
                    float d = ms - RevealRules.Shakes[i];
                    if (d >= 0 && d < 160) rot = Mathf.Sin(d / 160f * Mathf.PI * 3) * 10;
                }
                br.anchoredPosition = new Vector2(0, y);
                br.localRotation = Quaternion.Euler(0, 0, rot);
                glow.rectTransform.localRotation = Quaternion.Euler(0, 0, 45 + ms * 0.03f);
                if (!burst && ms >= RevealRules.Burst)
                {
                    burst = true;
                    m_audio.Play(best == Rarity.Legendary ? Sfx.Mythic : Sfx.Unlock);
                }
                if (burst)
                {
                    float f = (ms - RevealRules.Burst) / (RevealRules.Cards - RevealRules.Burst);
                    flash.color = new Color(1, 1, 1, Mathf.Clamp01(1 - Mathf.Abs(f * 2 - 1)));
                    br.localScale = Vector3.one * (1 + f * 0.6f);
                }
                if (ms >= RevealRules.Cards) break;
                yield return null;
            }
            if (m_skip && !burst) m_audio.Play(Sfx.Unlock);
            UnityEngine.Object.Destroy(scene.gameObject);
            UnityEngine.Object.Destroy(flash.gameObject);

            // ---- 카드 창 ----
            Image paper = Ui.Frame(root, "paper", 1.4f, "Dialog");
            paper.raycastTarget = true;
            RectTransform pr = paper.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(660, 0);
            Ui.Column(paper.transform, 12, 30, 30, 76, 30);
            paper.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Image ribbon = Ui.Frame(paper.transform, "ribbon_yellow", 1.3f, "Ribbon");
            ribbon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(460, 90));
            Ui.Fill(Ui.Line(ribbon.transform, Title(r), 34, Ui.Cream).rectTransform, 30, 18, 30, 8);

            if (r.Gems > 0) Ui.Line(paper.transform, Loc.Icon("gem") + " +" + r.Gems, 34, Ui.Ink, TextAlignmentOptions.Center, true);
            RectTransform grid = Ui.Rect(paper.transform, "Cards");
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(140, 196);
            g.spacing = new Vector2(10, 10);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            g.childAlignment = TextAnchor.UpperCenter;

            m_pops.Clear();
            EconState e = EconCache.State;
            for (int i = 0; i < order.Count; i++)
            {
                RectTransform card = Card(grid, e, order[i]);
                card.localScale = m_skip ? Vector3.one : Vector3.zero;
                m_pops.Add((card, delays[i]));
            }
            var legend = new System.Text.StringBuilder();
            for (int k = 0; k < Units.RarityKr.Length; k++)
                legend.Append("<color=#").Append(Units.RarityColor[k].ToString("x6")).Append(">■</color>").Append(Loc.Tl(Units.RarityKr[k])).Append(' ');
            Ui.Line(paper.transform, legend.ToString(), 20, Ui.Ink2, TextAlignmentOptions.Center, true);
            Button ok = Ui.Button(paper.transform, "btn_red", Loc.Tl("확인"), 32, () =>
            {
                UnityEngine.Object.Destroy(root.gameObject);
                m_open = null;
                Next();
            });
            Ui.Size(ok.gameObject, -1, 96);
            ok.interactable = m_skip;

            // 카드가 하나씩 뒤집힌다
            float c0 = Time.unscaledTime;
            int shown = 0;
            while (!m_skip && shown < m_pops.Count)
            {
                float ms = (Time.unscaledTime - c0) * 1000;
                for (int i = 0; i < m_pops.Count; i++)
                {
                    float d = ms - m_pops[i].at;
                    float s = Mathf.Clamp01(d / 200f);
                    m_pops[i].rt.localScale = Vector3.one * (s < 1 ? s * 1.15f : 1);
                    if (i == shown && d >= 0)
                    {
                        Rarity rr = Units.Get(order[i].Job).Rarity;
                        m_audio.Play(rr >= Rarity.Epic ? Sfx.Mythic : Sfx.Coin);
                        shown++;
                    }
                }
                yield return null;
            }
            foreach (var p in m_pops) if (p.rt != null) p.rt.localScale = Vector3.one;
            if (ok != null) ok.interactable = true;
        }

        RectTransform Card(Transform grid, EconState e, CardDrop d)
        {
            UnitDef u = Units.Get(d.Job);
            Color32 rc = Ui.Hex(Units.RarityColor[(int)u.Rarity]);
            Image card = Ui.Image(grid, null, rc, u.Id);
            Image inner = Ui.Frame(card.transform, "paper_dark", 1.8f, "Inner");
            Ui.Fill(inner.rectTransform, 4, 4, 4, 4);
            Ui.Column(inner.transform, 0, 6, 6, 8, 8, TextAnchor.MiddleCenter);
            Image pic = Ui.Image(inner.transform, m_cat.Unit(d.Job, 0)[1], Color.white, "Pic");
            pic.preserveAspect = true;
            Ui.Size(pic.gameObject, -1, 76);
            Ui.Line(inner.transform, "×" + d.N, 26, Ui.Gold);
            Ui.Line(inner.transform, Loc.Tl(u.Kr), 20, Ui.Cream);
            int lv = e.LevelOf(d.Job);
            int need = Units.CardsNeeded(u.Rarity, Math.Max(1, lv));
            int have = e.CardCount(d.Job);
            bool max = lv >= Units.MaxLevel;
            bool ready = lv > 0 && !max && have >= need;
            Image bar = Ui.Image(inner.transform, null, new Color32(0, 0, 0, 140), "Bar");
            Ui.Size(bar.gameObject, -1, 26);
            Image fill = Ui.Image(bar.transform, null, ready ? Ui.Green : Ui.Blue, "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(max ? 1 : Mathf.Clamp01(have / (float)need), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            Ui.Fill(Ui.Line(bar.transform, ready ? Loc.Tl("레벨업!") : max ? "MAX" : have + "/" + need, 18, Ui.Cream).rectTransform);
            if (d.Fresh) Ui.NewTag(card.transform);
            return card.rectTransform;
        }
    }
}
