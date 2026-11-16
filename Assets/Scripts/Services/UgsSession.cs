using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace DefenseBattle.Services
{
    /// <summary>
    /// UGS 초기화와 로그인. 앱에서 한 번만 초기화하고, 이미 로그인돼 있으면 다시 하지 않는다.
    /// 지금은 익명 로그인만 - Google Play Games 연동은 5단계 클라이언트 작업에서 붙인다.
    /// </summary>
    public static class UgsSession
    {
        static Task s_signIn;

        /// <summary>로그인된 플레이어 id. 로그인 전이면 null.</summary>
        public static string PlayerId =>
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerId
                : null;

        /// <summary>UGS 초기화 + 익명 로그인. 여러 번 불러도 한 번만 돈다 (실패하면 다음 호출에서 다시 시도).</summary>
        public static Task SignInAsync()
        {
            if (s_signIn == null || s_signIn.IsFaulted || s_signIn.IsCanceled)
                s_signIn = SignInCoreAsync();
            return s_signIn;
        }

        static async Task SignInCoreAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                var auth = AuthenticationService.Instance;
                if (auth.IsSignedIn)
                    return;

                await auth.SignInAnonymouslyAsync();
                Debug.Log($"[UGS] 익명 로그인 성공 - PlayerId {auth.PlayerId}");
            }
            catch (Exception e)
            {
                var root = RootCause(e);
                Debug.LogWarning($"[UGS] 로그인 실패 ({UnityServices.State}): {root.GetType().Name} {root.Message}");
                throw;
            }
        }

        /// <summary>UGS 초기화 예외는 AggregateException 으로 여러 겹 감싸져 온다 - 맨 안쪽 원인을 꺼낸다.</summary>
        static Exception RootCause(Exception e)
        {
            while (true)
            {
                if (e is AggregateException agg && agg.InnerExceptions.Count > 0)
                    e = agg.InnerExceptions[0];
                else if (e.InnerException != null)
                    e = e.InnerException;
                else
                    return e;
            }
        }
    }
}
