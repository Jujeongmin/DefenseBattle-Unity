using System.Collections.Generic;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 어떤 판을 열지 (main.ts Launch). 3단계(메타) 전까지는 덱 = 기본 3종, 연구 없음, 유닛 레벨 1.
    /// </summary>
    public sealed class BattleLaunch
    {
        public enum Mode { Stage, Endless }

        /// <summary>Battle 씬이 열 판 (로비·시작 화면이 바꾼다). 기본 1-1</summary>
        public static BattleLaunch Current = new BattleLaunch(Mode.Stage, "1-1");

        public readonly Mode Kind;
        /// <summary>캠페인 스테이지 id (무한이면 null)</summary>
        public readonly string StageId;

        public BattleLaunch(Mode kind, string stageId)
        {
            Kind = kind;
            StageId = kind == Mode.Stage ? stageId : null;
        }

        public GameOptions MakeOptions()
        {
            GameOptions o = Kind == Mode.Stage ? GameOptions.ForStage(Campaign.Get(StageId)) : new GameOptions();
            o.Deck = new List<Job>(Units.Starter);
            o.UseResearch = false;
            return o;
        }
    }
}
