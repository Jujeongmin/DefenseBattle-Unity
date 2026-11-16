using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 지금 쓰는 보상형 광고. 에디터는 바로 성공으로 흉내 (부활 흐름을 시험하려고),
    /// 기기는 RewardAdsBridge 가 AdMob 으로 바꾼다 (바꾸기 전엔 광고 없음 = 부활 버튼 숨김).
    /// </summary>
    public static class RewardAds
    {
        public static IRewardAds Service = Application.isEditor ? (IRewardAds)new EditorRewardAds() : new NoRewardAds();
    }
}
