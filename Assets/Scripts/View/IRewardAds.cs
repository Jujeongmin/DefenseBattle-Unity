using System;

namespace DefenseBattle.View
{
    /// <summary>보상형 광고 (원본 rewardedAvailable / showRewarded). 실제 AdMob 연결은 Ads 쪽 RewardAdsBridge 가 넣는다</summary>
    public interface IRewardAds
    {
        bool IsReady { get; }
        /// <summary>끝까지 보면 done(true)</summary>
        void Show(Action<bool> done);
    }
}
