using System.Threading.Tasks;

namespace DefenseBattle.View
{
    /// <summary>UGS 로그인 동작 (AccountLink 가 쓰는 만큼만). 실제는 UgsAuthOps, 테스트는 가짜</summary>
    public interface IAuthOps
    {
        bool IsSignedIn { get; }

        /// <summary>이 기기에 지난 로그인 세션이 남아 있음 (익명 로그인이 그 플레이어로 돌아간다)</summary>
        bool SessionTokenExists { get; }

        /// <summary>익명 로그인 (세션 토큰이 있으면 그 플레이어로)</summary>
        Task SignInAnonymously();

        Task SignInWithGpgs(string authCode);

        /// <summary>지금 플레이어에 Play 게임즈 계정이 연결돼 있음</summary>
        Task<bool> HasGpgs();

        /// <summary>지금 플레이어에 Play 게임즈 계정을 연결. false = 그 계정은 이미 다른 플레이어에 연결됨</summary>
        Task<bool> LinkGpgs(string authCode);

        /// <summary>로그아웃 (세션 토큰은 남긴다)</summary>
        void SignOut();
    }
}
