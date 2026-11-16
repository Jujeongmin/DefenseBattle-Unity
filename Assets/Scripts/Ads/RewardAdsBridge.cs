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
        if (!Application.isEditor) RewardAds.Service = new AdMobRewardAds();
    }

    sealed class AdMobRewardAds : IRewardAds
    {
        public bool IsReady => AdMobRewardedAdService.Shared.IsReady;
        public void Show(Action<bool> done) => AdMobRewardedAdService.Shared.Show(done);
    }
}
