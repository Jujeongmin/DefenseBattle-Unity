using System;
using DefenseBattle.View;
using UnityEngine;

/// <summary>
/// 기기에서 전투 화면의 보상형 광고(RewardAds)를 AdMob 으로 연결한다. 에디터는 View 쪽 흉내를 그대로 쓴다.
/// (광고 단위 ID 는 아직 예전 앱 값 - 7단계에서 새 앱 값으로)
/// </summary>
public sealed class RewardAdsBridge : MonoBehaviour
{
    void Awake()
    {
        if (Application.isEditor) return;
        RewardAds.Service = new AdMobRewardAds();
        // 처음 쓸 때 AdMob 초기화·광고 불러오기가 시작된다 - 판이 끝나는 순간이 아니라 씬이 열릴 때 미리 깨워
        // 첫 패배에도 부활 광고가 준비돼 있게 한다 (동의 창 순서는 7단계 출시 준비에서 정리)
        _ = AdMobRewardedAdService.Shared.IsReady;
    }

    sealed class AdMobRewardAds : IRewardAds
    {
        public bool IsReady => AdMobRewardedAdService.Shared.IsReady;
        public void Show(Action<bool> done) => AdMobRewardedAdService.Shared.Show(done);
    }
}
