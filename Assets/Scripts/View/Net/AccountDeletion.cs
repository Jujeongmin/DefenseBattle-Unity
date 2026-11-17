using System;
using System.Threading.Tasks;
using DefenseBattle.Core;
using DefenseBattle.Services;
using Unity.Services.Authentication;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 계정 삭제 (Google Play 계정 삭제 정책 - 원본에는 없음, 사용자 결정 2026-11-17):
    /// 서버 저장 값·랭킹(Cloud Code DeleteAccount) → 로그인 계정(Authentication) → 기기 기록(PlayerPrefs) 순서.
    /// 한 단계라도 실패하면 거기서 멈춰 false - 앞 단계는 다시 불러도 안전하다. 끝나면 앱을 다시 시작해야 새 계정으로 들어간다.
    /// </summary>
    public sealed class AccountDeletion
    {
        readonly Func<Task> m_server;
        readonly Func<Task> m_auth;
        readonly Action m_local;

        public AccountDeletion(Func<Task> deleteServerData, Func<Task> deleteLogin, Action clearDevice)
        {
            m_server = deleteServerData;
            m_auth = deleteLogin;
            m_local = clearDevice;
        }

        /// <summary>실제 서버·로그인·기기</summary>
        public static AccountDeletion Live() => new AccountDeletion(
            () => CloudClient.Call<bool>(CloudApi.DeleteAccount),
            () => AuthenticationService.Instance.DeleteAccountAsync(),
            () =>
            {
                PlayerPrefs.DeleteAll(); // 진행·닉네임·설정·로그인 세션 기록 - 처음 설치한 상태로
                PlayerPrefs.Save();
            });

        public async Task<bool> Run()
        {
            try { await m_server(); }
            catch (Exception ex)
            {
                Debug.LogWarning("[Account] 서버 데이터 삭제 실패: " + ex.Message);
                return false;
            }
            try { await m_auth(); }
            catch (Exception ex)
            {
                Debug.LogWarning("[Account] 로그인 계정 삭제 실패: " + ex.Message);
                return false;
            }
            m_local();
            Debug.Log("[Account] 계정 삭제 완료");
            return true;
        }
    }
}
