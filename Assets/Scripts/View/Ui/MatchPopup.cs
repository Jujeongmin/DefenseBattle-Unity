using System;
using System.Collections;
using System.Threading.Tasks;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 대전 상대 찾기 (menu.ts startMatchmaking · launchPvp): 첫 대전은 찾기 화면 없이 바로.
    /// 이후엔 '상대를 찾는 중' (취소 가능) → '매칭 완료!' 나 vs 상대 → 대전 시작.
    /// 실시간 대전을 쓸 수 있으면(PvpOnline) 실제 상대를 8초 찾고, 찾으면 VS 를 보여 주는 동안 Relay 로 연결해 서버가 정한 시각에 시작.
    /// 못 찾으면(또는 쓸 수 없으면 1.5~3.5초 연출 뒤) 실제 유저처럼 보이는 봇. 찾았는데 연결에 실패하면 안내하고 로비로.
    /// </summary>
    public sealed class MatchPopup
    {
        readonly Modal m_modal;
        readonly MonoBehaviour m_runner;
        readonly BattleAudio m_audio;
        const float FoundSec = 1.4f;
        readonly WaitForSecondsRealtime m_foundWait = new WaitForSecondsRealtime(FoundSec);
        Coroutine m_co;

        readonly Toast m_toast;

        public MatchPopup(Modal modal, MonoBehaviour runner, BattleAudio audio, Toast toast)
        {
            m_toast = toast;
            m_modal = modal;
            m_runner = runner;
            m_audio = audio;
        }

        public bool Busy => m_co != null;

        public void Start(EconState e, Action<IPvpOpponent> launch)
        {
            if (m_co != null) return;
            BotOpponent opp = BotOpponent.Create(e);
            if (PvpRules.IsFirstMatch(e)) { launch(opp); return; } // 첫 대전(배우는 판)
            m_co = m_runner.StartCoroutine(Run(e, opp, launch));
        }

        public void Stop()
        {
            if (m_co != null) m_runner.StopCoroutine(m_co);
            m_co = null;
        }

        IEnumerator Run(EconState e, BotOpponent opp, Action<IPvpOpponent> launch)
        {
            bool cancelled = false;
            RectTransform body = m_modal.Custom(Loc.Tl("상대를 찾는 중..."), () => cancelled = true);
            Image spin = Ui.Icon(body, "loading", 72);
            TextMeshProUGUI sec = Ui.Line(body, Loc.Tl("{0}초", 0), 26, Ui.Ink, TextAlignmentOptions.Center, true);
            Ui.Line(body, Loc.Icon("trophy") + " " + e.Pvp.Trophies + " " + Loc.Tl("근처의 상대를 찾고 있어요"), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            Ui.Size(Ui.Button(body, "btn_gray", Loc.Tl("취소"), 28, () => { cancelled = true; m_modal.Close(); }).gameObject, -1, 84);

            if (PvpOnline.Available)
            {
                MatchSearch search = PvpOnline.NewSearch();
                double waited = 0;
                Task<MatchInfo> find = search.Find(PvpOnline.BotFallbackSec, () => cancelled || !m_modal.IsOpen, s => waited = s);
                int shownSec = 0;
                double spinT = 0;
                while (!find.IsCompleted)
                {
                    spinT += Time.unscaledDeltaTime;
                    spin.rectTransform.localRotation = Quaternion.Euler(0, 0, -(int)(spinT * 8) * 45f);
                    if ((int)waited != shownSec) { shownSec = (int)waited; sec.text = Loc.Tl("{0}초", shownSec); }
                    yield return null;
                }
                if (cancelled || !m_modal.IsOpen) { m_co = null; yield break; }
                MatchInfo found = find.IsFaulted ? null : find.Result;
                if (found != null)
                {
                    ShowFound(e, found.Opponent?.Nick ?? "", found.Opponent?.Trophies ?? 0);
                    Task<OnlineOpponent> conn = search.Connect(found, () => !m_modal.IsOpen);
                    float shown = 0;
                    while (!conn.IsCompleted || shown < FoundSec) // VS 는 적어도 1.4초 보여 준다
                    {
                        shown += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    m_co = null;
                    OnlineOpponent online = conn.IsFaulted ? null : conn.Result;
                    if (!m_modal.IsOpen) { online?.Leave(); yield break; } // 그 사이 닫았으면 취소
                    m_modal.Close();
                    if (online == null) { m_toast.Show(Loc.Tl("대전 서버 연결에 실패했어요")); yield break; }
                    launch(online);
                    yield break;
                }
            }
            else
            {
                double wait = PvpRules.SearchSeconds(new SystemRandom());
                double t = 0;
                int shown = 0;
                while (t < wait)
                {
                    if (cancelled || !m_modal.IsOpen) { m_co = null; yield break; }
                    t += Time.unscaledDeltaTime;
                    spin.rectTransform.localRotation = Quaternion.Euler(0, 0, -(int)(t * 8) * 45f); // 8칸 픽셀 스피너
                    if ((int)t != shown) { shown = (int)t; sec.text = Loc.Tl("{0}초", shown); }
                    yield return null;
                }
            }

            ShowFound(e, opp.Nick, opp.Trophies);
            yield return m_foundWait;
            m_co = null;
            if (!m_modal.IsOpen) yield break; // 그 사이 닫았으면 취소
            m_modal.Close();
            launch(opp);
        }

        void ShowFound(EconState e, string oppNick, int oppTrophies)
        {
            RectTransform body = m_modal.Custom(Loc.Tl("매칭 완료!"));
            RectTransform vs = Ui.Rect(body, "Vs");
            Ui.Size(vs.gameObject, -1, 120);
            HorizontalLayoutGroup row = Ui.Row(vs, 10);
            row.childForceExpandWidth = true;
            Side(vs, Profile.Nick, e.Pvp.Trophies);
            Ui.Size(Ui.Line(vs, "VS", 40, Ui.Red, TextAlignmentOptions.Center, true).gameObject, 90, -1);
            Side(vs, oppNick, oppTrophies);
            Ui.Line(body, Loc.Tl("곧 전투가 시작됩니다"), 22, Ui.Ink2, TextAlignmentOptions.Center, true);
            m_audio.Play(Sfx.Send);
        }

        static void Side(Transform parent, string nick, int trophies)
        {
            RectTransform col = Ui.Rect(parent, "Side");
            Ui.Size(col.gameObject, -1, -1, 1);
            Ui.Column(col, 4, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Ui.Line(col, "<noparse>" + nick + "</noparse>", 28, Ui.Ink, TextAlignmentOptions.Center, true);
            PvpTier tier = Arenas.Of(trophies);
            Ui.Line(col, Loc.Icon("trophy") + " " + trophies, 24, Ui.Hex(tier.Color), TextAlignmentOptions.Center, true);
        }
    }
}
