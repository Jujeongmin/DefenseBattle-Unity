using System;
using System.Collections;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 결제 팝업 (offers.ts popup · afterMatch · showShortage): 판이 끝난 뒤 한정 상품 / 유닛 패키지 등장, 보석 부족 상품.
    /// 로비와 전투 결과 화면 위 모두에 뜨도록 자기 칸(OfferHost)에 그린다. 언제 뜨는지는 OfferRules (Core).
    /// </summary>
    public sealed class OfferPopup
    {
        readonly Modal m_modal;
        readonly SpriteCatalog m_cat;
        readonly ProductBuy m_buy;
        readonly MonoBehaviour m_runner;
        readonly WaitForSecondsRealtime m_second = new WaitForSecondsRealtime(1f);
        readonly WaitForSecondsRealtime m_delay = new WaitForSecondsRealtime(0.7f);
        Coroutine m_timer;
        Action m_done;

        public OfferPopup(Transform host, SpriteCatalog cat, ProductBuy buy, MonoBehaviour runner)
        {
            m_modal = new Modal(host);
            m_cat = cat;
            m_buy = buy;
            m_runner = runner;
        }

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public bool IsOpen => m_modal.IsOpen;

        /// <summary>판이 끝난 뒤 (결과 화면이 먼저 보이게 0.7초 뒤). 판마다 최대 하나: 승급 > 유닛 패키지 > 새 챕터 > 연패</summary>
        public void AfterMatch(bool tierUp, bool chapterUp, bool losing)
        {
            EconState e = EconCache.State;
            LocalMetaData m = LocalMeta.Data;
            string kind = OfferRules.AfterMatch(e, m.Offers, m.OfferShownAt, Now, e.Season.Id, tierUp, chapterUp, losing, out string id);
            LocalMeta.Save(); // 유닛 패키지를 조용히 등장 처리만 했어도 기록
            if (kind == null) return;
            string title, why;
            switch (kind)
            {
                case "pack":
                    string name = Loc.Tl(Units.Get(PackUnit(id)).Kr);
                    title = Loc.Tl("{0} 패키지 등장!", name);
                    why = Loc.Tl("곧 트로피 로드에서 {0}이(가) 열려요. 지금 바로 받아 Lv5로 시작하세요 · 상점에서도 살 수 있어요", name);
                    break;
                case "arena":
                    title = Loc.Tl("아레나 승급 축하!");
                    why = Loc.Tl("새 아레나에 올랐어요. 승급 기념 특가");
                    break;
                case "chapter":
                    title = Loc.Tl("새 챕터 열림!");
                    why = Loc.Tl("다음 챕터는 더 강한 몹이 나와요. 미리 준비하세요");
                    break;
                default:
                    title = Loc.Tl("다시 일어서기!");
                    why = Loc.Tl("연속으로 졌어요. 소환권과 보석으로 덱을 다시 키워 보세요");
                    break;
            }
            m_runner.StartCoroutine(Later(id, title, why));
        }

        IEnumerator Later(string id, string title, string why)
        {
            yield return m_delay;
            Show(id, title, why);
        }

        /// <summary>보석 부족: 그 유닛 패키지(살 수 있으면) 또는 모자란 만큼을 채우는 가장 작은 보석 상품</summary>
        public void Shortage(int need, Job? job = null)
        {
            EconState e = EconCache.State;
            string id = OfferRules.ShortageProduct(e, LocalMeta.Data.OfferShownAt, need, job, e.Season.Id);
            if (id == null) return;
            if (Products.Get(id).Category == "unit")
                Show(id, Loc.Tl("{0} 바로 받기", Loc.Tl(Units.Get(job.Value).Kr)), Loc.Tl("보석 {0}개가 부족해요. 패키지로 바로 해금하고 Lv5까지 올려요", Ui.Num(need)));
            else
                Show(id, Loc.Tl("보석이 부족해요"), Loc.Tl("보석 {0}개가 더 필요해요", Ui.Num(need)));
        }

        static Job PackUnit(string id)
        {
            foreach (var kv in Products.Get(id).Grant.Units) return kv.Key;
            return Job.Ranger;
        }

        /// <summary>상품 팝업 하나 (한정 상품이면 남은 시간도 1초마다)</summary>
        public void Show(string id, string title, string why, Action done = null)
        {
            ProductDef p = Products.Get(id);
            if (p == null) return;
            Finish(); // 떠 있던 팝업의 done 을 먼저
            m_done = done;
            RectTransform body = m_modal.Custom(title, Finish);
            Ui.Text(body, why, 24, Ui.Ink2, TextAlignmentOptions.Center, true);

            Image box = Ui.Frame(body, "paper_dark", 1.8f, "Box");
            Ui.Column(box.transform, 6, 20, 20, 22, 20, TextAnchor.UpperCenter);
            ProductUi.EffBadge(box.transform, p);
            RectTransform pic = Ui.Rect(box.transform, "PicRow");
            Ui.Size(pic.gameObject, -1, 140);
            Ui.Row(pic, 0);
            ProductUi.Picture(pic, p, 140);
            Ui.Line(box.transform, Loc.Tl(p.Kr), 30, Ui.Cream);
            ProductUi.GrantRows(box.transform, m_cat, p);

            if (p.Category == "offer")
            {
                TextMeshProUGUI timer = Ui.Line(body, "", 24, Ui.Ink, TextAlignmentOptions.Center, true);
                m_timer = m_runner.StartCoroutine(Tick(id, timer));
            }
            Ui.Size(Ui.Button(body, "btn_gold", Store.Price(p), 32, () => m_buy.Buy(id, ok => { if (ok) Close(); })).gameObject, -1, 92);
            Ui.Size(Ui.Button(body, "btn_gray", Loc.Tl("다음에"), 26, Close).gameObject, -1, 76);
        }

        IEnumerator Tick(string id, TextMeshProUGUI timer)
        {
            var offers = LocalMeta.Data.Offers;
            while (timer != null)
            {
                long left = OfferRules.MsLeft(offers, id, Now);
                timer.text = Loc.Icon("timer") + " " + ProductUi.Clock(left) + " " + Loc.Tl("남음");
                if (left <= 0) break;
                yield return m_second;
            }
            m_timer = null;
        }

        public void Close()
        {
            m_modal.Close();
            Finish();
        }

        /// <summary>시간 멈추고 done 한 번</summary>
        void Finish()
        {
            if (m_timer != null) m_runner.StopCoroutine(m_timer);
            m_timer = null;
            Action d = m_done;
            m_done = null;
            d?.Invoke();
        }
    }
}
