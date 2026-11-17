using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>대전 결과 (pvp.ts PvpResult). 트로피·보상은 서버가 정한 값 - 서버 결과가 없으면 Delta 0, Reward null</summary>
    public sealed class PvpOutcome
    {
        public bool Won;
        public int Delta;
        public int Trophies;
        public PvpRewardResult Reward;
    }
}
