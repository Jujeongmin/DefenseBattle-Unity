using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 어떤 판을 열지 (main.ts Launch): 캠페인 스테이지 / 무한 모드 + 골드 부스터.
    /// 덱·유닛 레벨·연구는 서버 경제 상태(progress.ts stageOptions / endlessOptions)에서 가져온다.
    /// </summary>
    public sealed class BattleLaunch
    {
        public enum Mode { Stage, Endless }

        public readonly Mode Kind;
        /// <summary>캠페인 스테이지 id (무한이면 null)</summary>
        public readonly string StageId;
        /// <summary>골드 부스터 사용 (시작 골드 2배 - 판을 열 때 서버에서 1개 차감)</summary>
        public readonly bool GoldBooster;

        public BattleLaunch(Mode kind, string stageId, bool goldBooster = false)
        {
            Kind = kind;
            StageId = kind == Mode.Stage ? stageId : null;
            GoldBooster = goldBooster;
        }

        public GameOptions MakeOptions(EconState e)
            => Kind == Mode.Stage ? Progress.StageOptions(e, StageId, GoldBooster) : Progress.EndlessOptions(e, GoldBooster);

        /// <summary>같은 판 다시 (부스터는 다시 쓰지 않는다 - main.ts 다시 하기 goldBooster:false)</summary>
        public BattleLaunch Again() => new BattleLaunch(Kind, StageId);
    }
}
