using System.Threading.Tasks;
using DefenseBattle.View;
using Unity.Services.Authentication;

namespace DefenseBattle.Services
{
    /// <summary>IAuthOps 의 실제 구현 (UGS Authentication)</summary>
    public sealed class UgsAuthOps : IAuthOps
    {
        readonly IAuthenticationService m_auth;

        public UgsAuthOps(IAuthenticationService auth) { m_auth = auth; }

        public bool IsSignedIn => m_auth.IsSignedIn;
        public bool SessionTokenExists => m_auth.SessionTokenExists;

        public Task SignInAnonymously() => m_auth.SignInAnonymouslyAsync();

        public Task SignInWithGpgs(string authCode) => m_auth.SignInWithGooglePlayGamesAsync(authCode);

        public async Task<bool> HasGpgs()
        {
            PlayerInfo info = await m_auth.GetPlayerInfoAsync();
            return !string.IsNullOrEmpty(info?.GetGooglePlayGamesId());
        }

        public async Task<bool> LinkGpgs(string authCode)
        {
            try
            {
                await m_auth.LinkWithGooglePlayGamesAsync(authCode);
                return true;
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return false;
            }
        }

        public void SignOut() => m_auth.SignOut(false);
    }
}
