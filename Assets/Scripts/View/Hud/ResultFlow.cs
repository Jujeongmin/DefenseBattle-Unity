using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 판 끝 화면 구성 (main.ts onGameOver / finishSolo / exitGame).
    /// 실패 + 부활 가능 + 광고 준비됨 → '위기!' (부활 / 포기), 아니면 최종 결과.
    /// 3단계(메타) 전이라 보상·다음 스테이지·튜토리얼 분기는 아직 없다.
    /// </summary>
    public static class ResultFlow
    {
        public static ResultView ForGameOver(GameOverData d, Game g, BattleLaunch l, bool adReady)
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
            return Final(d, l);
        }

        public static ResultView Final(GameOverData d, BattleLaunch l)
        {
            var v = new ResultView { Win = d.Cleared };
            if (l.Kind == BattleLaunch.Mode.Stage)
            {
                StageDef st = Campaign.Get(l.StageId);
                v.Title = d.Cleared ? Loc.Tl("스테이지 클리어!") : Loc.Tl("실패...");
                v.Sub = d.Cleared
                    ? Loc.Tl("{0} · 최대 필드 몹 {1}마리{2}", l.StageId, d.PeakMobs, "")
                    : d.Reason + " · Wave " + d.Wave + "/" + st.Waves;
                if (d.Cleared) v.Stars = Campaign.StarsFor(d.PeakMobs);
                // 다시 하기는 실패했을 때만 (클리어면 다음 스테이지 / 로비로)
                if (!d.Cleared) v.Buttons.Add(new ResultButton { Label = Loc.Tl("다시 하기"), Style = ResultStyle.Red, Action = ResultAction.Retry });
                v.Buttons.Add(new ResultButton { Label = Loc.Tl("로비로"), Style = ResultStyle.Plain, Action = ResultAction.Lobby });
                return v;
            }
            v.Title = d.Cleared ? Loc.Tl("50웨이브 클리어!") : Loc.Tl("게임 오버");
            // 최고 기록은 3단계(저장) 전까지 이번 판 기록
            v.Sub = Loc.Tl("{0} · 최고 기록 {1}웨이브", d.Reason, d.Wave);
            v.Rewards.Add("Wave " + d.Wave);
            v.Buttons.Add(new ResultButton { Label = Loc.Tl("다시 하기"), Style = ResultStyle.Red, Action = ResultAction.Retry });
            v.Buttons.Add(new ResultButton { Label = Loc.Tl("로비로"), Style = ResultStyle.Plain, Action = ResultAction.Lobby });
            return v;
        }

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
