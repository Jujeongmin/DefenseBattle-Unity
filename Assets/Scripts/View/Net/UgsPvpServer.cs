using System.Threading.Tasks;
using DefenseBattle.Core;
using DefenseBattle.Services;

namespace DefenseBattle.View
{
    /// <summary>IPvpServer 의 실제 구현 (Cloud Code 실시간 대전 함수)</summary>
    public sealed class UgsPvpServer : IPvpServer
    {
        public string PlayerId => UgsSession.PlayerId;

        public Task<QueueStatus> Queue() => CloudClient.Call<QueueStatus>(CloudApi.PvpQueue);
        public Task<QueueStatus> Poll() => CloudClient.Call<QueueStatus>(CloudApi.PvpPoll);
        public Task Cancel() => CloudClient.Call<bool>(CloudApi.PvpCancel);

        public Task<MatchState> Ready(string matchId, string joinCode)
            => CloudClient.Call<MatchState>(CloudApi.PvpReady, CloudClient.Args("matchId", matchId, "relayJoinCode", joinCode ?? ""));

        public Task<MatchState> Ping(string matchId) => CloudClient.Call<MatchState>(CloudApi.PvpPing, CloudClient.Args("matchId", matchId));
        public Task<PvpMatchResult> Defeat(string matchId) => CloudClient.Call<PvpMatchResult>(CloudApi.PvpDefeat, CloudClient.Args("matchId", matchId));
        public Task<PvpMatchResult> Leave(string matchId) => CloudClient.Call<PvpMatchResult>(CloudApi.PvpLeave, CloudClient.Args("matchId", matchId));
        public Task<PvpMatchResult> ClaimAway(string matchId) => CloudClient.Call<PvpMatchResult>(CloudApi.PvpClaimAway, CloudClient.Args("matchId", matchId));
        public Task SyncEcon() => CloudClient.Sync();
    }
}
