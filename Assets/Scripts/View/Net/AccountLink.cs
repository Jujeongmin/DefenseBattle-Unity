using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 로그인 + Play 게임즈 계정 연결 (spec 대응표: 원본 Verse8 계정 → UGS 익명 + Google Play 게임즈 연결).
    /// - 이 기기에 세션이 없고 Play 게임즈 코드가 있으면 그 계정으로 로그인 (새 기기에서 진행 복원)
    /// - 그 밖엔 익명 로그인 (남은 세션이면 그 플레이어) 뒤, Play 게임즈가 아직 안 붙었으면 연결
    /// - 그 Play 게임즈 계정이 이미 다른 플레이어에 연결돼 있으면 그 플레이어로 바꾼다 (onAccountChange 로 기기 캐시를 비운다)
    /// Play 게임즈가 안 되면(코드 없음·실패) 익명으로 그대로 진행한다.
    /// </summary>
    public sealed class AccountLink
    {
        readonly IAuthOps m_auth;
        readonly Action m_onAccountChange;

        public AccountLink(IAuthOps auth, Action onAccountChange)
        {
            m_auth = auth;
            m_onAccountChange = onAccountChange;
        }

        /// <param name="authCode">Play 게임즈 서버 인증 코드 (한 번용). null 을 주면 Play 게임즈를 쓰지 않는다</param>
        public async Task Run(Func<Task<string>> authCode)
        {
            string code = await Code(authCode);
            if (!m_auth.IsSignedIn)
            {
                if (code != null && !m_auth.SessionTokenExists)
                {
                    if (!await TrySignInGpgs(code)) await m_auth.SignInAnonymously(); // 코드는 썼다 - 연결은 다음 실행에서
                    return;
                }
                await m_auth.SignInAnonymously();
            }
            if (code == null) return;

            try
            {
                if (await m_auth.HasGpgs()) return;
                if (await m_auth.LinkGpgs(code))
                {
                    Debug.Log("[UGS] Play 게임즈 계정 연결됨");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UGS] Play 게임즈 연결 실패 (익명으로 계속): " + ex.Message);
                return;
            }

            // 이 Play 게임즈 계정은 다른 플레이어 것 (다른 기기에서 하던 진행) - 그쪽으로 바꾼다. 코드는 연결 시도에 썼으니 새로 받는다
            string fresh = await Code(authCode);
            if (fresh == null) return;
            m_auth.SignOut();
            if (await TrySignInGpgs(fresh))
            {
                m_onAccountChange?.Invoke();
                Debug.Log("[UGS] Play 게임즈 계정의 플레이어로 바꿈");
                return;
            }
            await m_auth.SignInAnonymously(); // 남은 세션 토큰으로 원래 플레이어
        }

        async Task<bool> TrySignInGpgs(string code)
        {
            try
            {
                await m_auth.SignInWithGpgs(code);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UGS] Play 게임즈 로그인 실패 (익명으로): " + ex.Message);
                return false;
            }
        }

        static async Task<string> Code(Func<Task<string>> authCode)
        {
            if (authCode == null) return null;
            try
            {
                string c = await authCode();
                return string.IsNullOrEmpty(c) ? null : c;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UGS] Play 게임즈 인증 안 됨: " + ex.Message);
                return null;
            }
        }
    }
}
