using System;
using System.Threading.Tasks;
using DefenseBattle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>
    /// 접속 화면 (spec Boot): 로딩 배경 + 상태 글자. 서버 로그인·경제 동기화를 3번까지 다시 시도하고(platform.ts),
    /// 안 되면 인터넷 안내 + 다시 시도 버튼. 경제가 서버 권위라 연결 없이는 시작하지 않는다.
    /// </summary>
    public sealed class BootPanel : MonoBehaviour
    {
        [SerializeField] GameObject m_root;
        [SerializeField] TextMeshProUGUI m_status;
        [SerializeField] Button m_retry;

        const int Tries = 3;
        Action m_onReady;

        void Awake() => m_retry.onClick.AddListener(() => _ = Run(m_onReady));

        public async Task Run(Action onReady)
        {
            m_onReady = onReady;
            m_root.SetActive(true);
            m_retry.gameObject.SetActive(false);
            m_status.text = Loc.Tl("서버에 접속하는 중...");
            for (int i = 1; i <= Tries; i++)
            {
                try
                {
                    await CloudClient.Sync();
                    m_root.SetActive(false);
                    onReady?.Invoke();
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Boot] 접속 실패 " + i + "/" + Tries + ": " + e.Message);
                    if (i < Tries) await Task.Delay(800 * i);
                }
            }
            m_status.text = Loc.Tl("서버에 연결할 수 없어요.\n인터넷 연결을 확인해 주세요.");
            m_retry.gameObject.SetActive(true);
        }
    }
}
