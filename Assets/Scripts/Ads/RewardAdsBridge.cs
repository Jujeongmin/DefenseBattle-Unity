using System;
using DefenseBattle.View;
using UnityEngine;

/// <summary>
/// 기기에서 전투 화면의 보상형 광고(RewardAds)를 AdMob 으로 연결한다. 에디터는 View 쪽 흉내를 그대로 쓴다.
/// 광고 단위 ID 는 릴리스 빌드 때만 들어온다 (AdUnitConfig), 광고 요청은 동의 확인 뒤 (AdConsentManager).
/// </summary>
public sealed class RewardAdsBridge : MonoBehaviour
{
    void Awake()
    {
        if (Application.isEditor) return;
        RewardAds.Service = new AdMobRewardAds();
        // 씬이 열릴 때 미리 깨워 둔다 - 동의 확인이 끝나면 바로 초기화·광고 불러오기가 시작돼 첫 패배에도 부활 광고가 준비돼 있게
        _ = AdMobRewardedAdService.Shared.IsReady;
    }

    sealed class AdMobRewardAds : IRewardAds
    {
        public bool IsReady => AdMobRewardedAdService.Shared.IsReady;
        public void Show(Action<bool> done) => AdMobRewardedAdService.Shared.Show(done);
    }
}
