using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 판 끝 화면 구성 (main.ts onGameOver / finishSolo / exitGame).
    /// 실패 + 부활 가능 + 광고 준비됨 → '위기!' (부활 / 포기), 아니면 최종 결과.
    /// 보상은 서버(RunEnd) 응답, 다음 스테이지 해금은 반영된 경제 상태로 판단한다.
    /// </summary>
    public static class ResultFlow
    {
        public static ResultView ForGameOver(GameOverData d, Game g, bool adReady)
        {
            if (!d.Cleared && g.CanRevive() && adReady)
            {
                var v = new ResultView
                {
                    Win = false,
                    Title = Loc.Tl("위기!"),
                    Sub = d.Reason,
                    Extra = Loc.Tl("<div class=\"fine\">광고 보고 몹을 모두 지우고 계속 (1회)</div>"),
                };
                v.Buttons.Add(new ResultButton { Label = Loc.Tl("{0} 부활하고 계속하기", Loc.Icon("tv")), Style = ResultStyle.Green, Action = ResultAction.Revive });
                v.Buttons.Add(new ResultButton { Label = Loc.Tl("포기하기"), Style = ResultStyle.Plain, Action = ResultAction.GiveUp });
                return v;
            }
            return null;
        }

        /// <summary>최종 결과 (main.ts finishSolo). e = 서버가 결과를 반영한 뒤의 경제 상태</summary>
        public static ResultView Final(GameOverData d, BattleLaunch l, RunOutcome o, EconState e)
        {
            var v = new ResultView { Win = d.Cleared };
            if (l.Kind == BattleLaunch.Mode.Stage)
            {
                StageDef st = Campaign.Get(l.StageId);
                StageEndResult res = o.Stage ?? new StageEndResult();
                v.Title = d.Cleared ? Loc.Tl("스테이지 클리어!") : Loc.Tl("실패...");
                string note = res.FirstClear ? Loc.Tl(" · 첫 클리어!") : res.NewBest ? Loc.Tl(" · 별 기록 갱신!") : "";
                v.Sub = d.Cleared
                    ? Loc.Tl("{0} · 최대 필드 몹 {1}마리{2}", l.StageId, d.PeakMobs, note)
                    : d.Reason + " · Wave " + d.Wave + "/" + st.Waves;
                if (d.Cleared) v.Stars = Campaign.StarsFor(d.PeakMobs);
                if (res.Crystals > 0) v.Rewards.Add(Loc.Icon("gem") + " +" + res.Crystals);
                if (res.Unlocked != null)
                    v.Extra = Loc.Tl("새 유닛 해금:") + " " + Loc.Tl(Units.Get(res.Unlocked.Value).Kr) + "\n" + Loc.Tl("덱에 넣어 보세요");
                if (res.Crystals > 0 && o.AdReady) v.Buttons.Add(DoubleButton(res.Crystals));
                StageDef next = st.Index + 1 < Campaign.Stages.Length ? Campaign.Stages[st.Index + 1] : null;
                if (d.Cleared && next != null && Progress.StageUnlocked(e, next.Id))
                    v.Buttons.Add(new ResultButton { Label = Loc.Tl("{0} 다음 스테이지 {1}", Loc.Icon("play"), next.Id), Style = ResultStyle.Red, Action = ResultAction.Next, StageId = next.Id });
                // 다시 하기는 실패했을 때만. 튜토리얼 판에서 지면 로비로 없이 바로 1-1 다시
                if (!d.Cleared && o.TutorialRun) v.Buttons.Add(new ResultButton { Label = Loc.Tl("다시 하기"), Style = ResultStyle.Red, Action = ResultAction.Retry });
                else if (!d.Cleared) v.Buttons.Add(new ResultButton { Label = Loc.Tl("다시 하기"), Style = ResultStyle.Red, Action = ResultAction.StageInfo, StageId = l.StageId });
                if (!(o.TutorialRun && !d.Cleared)) v.Buttons.Add(new ResultButton { Label = Loc.Tl("로비로"), Style = ResultStyle.Plain, Action = ResultAction.Lobby });
                return v;
            }
            v.Title = d.Cleared ? Loc.Tl("50웨이브 클리어!") : Loc.Tl("게임 오버");
            v.Sub = Loc.Tl("{0} · 최고 기록 {1}웨이브", d.Reason, o.BestWave);
            v.Rewards.Add("Wave " + d.Wave);
            v.Rewards.Add(Loc.Icon("gem") + " +" + o.Crystals);
            if (o.Crystals > 0 && o.AdReady) v.Buttons.Add(DoubleButton(o.Crystals));
            v.Buttons.Add(new ResultButton { Label = Loc.Tl("다시 하기"), Style = ResultStyle.Red, Action = ResultAction.Retry });
            v.Buttons.Add(new ResultButton { Label = Loc.Tl("로비로"), Style = ResultStyle.Plain, Action = ResultAction.Lobby });
            return v;
        }

        static ResultButton DoubleButton(int crystals) => new ResultButton
        {
            Label = Loc.Tl("{0} 광고 보고 보상 2배 ({1}+{2})", Loc.Icon("tv"), Loc.Icon("gem"), crystals),
            Style = ResultStyle.Blue,
            Action = ResultAction.Double,
        };

        /// <summary>판 도중 나가기 확인 (main.ts exitGame confirmModal)</summary>
        public static ResultView ExitConfirm()
        {
            var v = new ResultView { Title = Loc.Tl("나가기"), Sub = Loc.Tl("진행 중인 판을 포기하고 로비로 갈까요?") };
            v.Buttons.Add(new ResultButton { Label = Loc.Tl("나가기"), Style = ResultStyle.Red, Action = ResultAction.Lobby });
            v.Buttons.Add(new ResultButton { Label = Loc.Tl("취소"), Style = ResultStyle.Plain, Action = ResultAction.Close });
            return v;
        }
    }
}
