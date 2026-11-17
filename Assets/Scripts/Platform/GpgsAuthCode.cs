using System;
using System.Threading.Tasks;
using DefenseBattle.Services;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Google Play 게임즈 로그인 → UGS 에 넘길 서버 인증 코드 (안드로이드 기기에서만, 에디터는 익명 로그인).
/// 첫 씬 전에 UgsSession.AuthCode 에 등록한다. 코드는 한 번용이라 부를 때마다 새로 받는다.
/// </summary>
public static class GpgsAuthCode
{
    const int TimeoutMs = 15000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayGamesPlatform.Activate();
        UgsSession.AuthCode = Get;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static async Task<string> Get()
    {
        var tcs = new TaskCompletionSource<string>();
        PlayGamesPlatform.Instance.Authenticate(status =>
        {
            if (status != SignInStatus.Success)
            {
                Debug.Log("[GPGS] 로그인 안 됨: " + status);
                tcs.TrySetResult(null);
                return;
            }
            PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => tcs.TrySetResult(string.IsNullOrEmpty(code) ? null : code));
        });
        Task first = await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMs));
        if (first != tcs.Task) Debug.LogWarning("[GPGS] 응답 없음 - 익명으로 진행");
        return first == tcs.Task ? tcs.Task.Result : null;
    }
#endif
}
