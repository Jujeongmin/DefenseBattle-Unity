using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 어떤 판을 열지 (main.ts Launch): 캠페인 스테이지 / 무한 모드 + 골드 부스터 / 대전(상대).
    /// 덱·유닛 레벨·연구는 서버 경제 상태(progress.ts stageOptions / endlessOptions, main.ts launch pvp)에서 가져온다.
    /// </summary>
    public sealed class BattleLaunch
    {
        public enum Mode { Stage, Endless, Pvp }

        public readonly Mode Kind;
        /// <summary>캠페인 스테이지 id (무한이면 null)</summary>
        public readonly string StageId;
        /// <summary>골드 부스터 사용 (시작 골드 2배 - 판을 열 때 서버에서 1개 차감)</summary>
        public readonly bool GoldBooster;
        /// <summary>대전 상대 (대전이 아니면 null)</summary>
        public readonly IPvpOpponent Opponent;

        public BattleLaunch(Mode kind, string stageId, bool goldBooster = false)
        {
            Kind = kind;
            StageId = kind == Mode.Stage ? stageId : null;
            GoldBooster = goldBooster;
        }

        BattleLaunch(IPvpOpponent opponent)
        {
            Kind = Mode.Pvp;
            Opponent = opponent;
        }

        /// <summary>대전 (부스터는 일반 모드 전용)</summary>
        public static BattleLaunch Pvp(IPvpOpponent opponent) => new BattleLaunch(opponent);

        public GameOptions MakeOptions(EconState e)
        {
            switch (Kind)
            {
                case Mode.Stage: return Progress.StageOptions(e, StageId, GoldBooster);
                case Mode.Pvp: return PvpRules.PlayerOptions(e, Opponent.Seed);
                default: return Progress.EndlessOptions(e, GoldBooster);
            }
        }

        /// <summary>같은 판 다시 (부스터는 다시 쓰지 않는다 - main.ts 다시 하기 goldBooster:false). 대전은 매칭부터 다시</summary>
        public BattleLaunch Again() => new BattleLaunch(Kind, StageId);
    }
}
