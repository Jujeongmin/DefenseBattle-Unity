using UnityEngine;

namespace DefenseBattle.Services
{
    /// <summary>
    /// 씬에 두면 시작할 때 UGS 로그인을 시도하고 결과를 로그로 남긴다 (0단계 확인용, Boot 씬이 생기면 그쪽이 대신한다).
    /// </summary>
    public class UgsLoginProbe : MonoBehaviour
    {
        async void Start()
        {
            try
            {
                await UgsSession.SignInAsync();
            }
            catch
            {
                // UgsSession 이 이미 경고를 남겼다.
            }
        }
    }
}
