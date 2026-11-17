using System.Threading.Tasks;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>실시간 대전 서버 함수 (Cloud Code PvpQueue ~ PvpClaimAway). 실제는 UgsPvpServer, 테스트는 가짜</summary>
    public interface IPvpServer
    {
        /// <summary>내 플레이어 id (결과에서 내 몫을 찾는다)</summary>
        string PlayerId { get; }
        Task<QueueStatus> Queue();
        Task<QueueStatus> Poll();
        Task Cancel();
        /// <summary>준비 완료 (호스트는 Relay 참가 코드를 같이)</summary>
        Task<MatchState> Ready(string matchId, string joinCode);
        /// <summary>경기 중 몇 초마다 - 아직 있다고 알리고 상태·결과를 받는다</summary>
        Task<MatchState> Ping(string matchId);
        Task<PvpMatchResult> Defeat(string matchId);
        Task<PvpMatchResult> Leave(string matchId);
        /// <summary>상대가 오래 응답 없음 → 승리 요청 (서버가 시간을 확인, 아니면 null)</summary>
        Task<PvpMatchResult> ClaimAway(string matchId);
        /// <summary>결과 뒤 경제 상태 다시 받기</summary>
        Task SyncEcon();
    }
}
