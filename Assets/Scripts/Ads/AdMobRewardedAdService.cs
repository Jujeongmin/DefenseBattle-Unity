using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/// <summary>AdMob 보상형 광고 하나를 불러 두고 보여 준다 (RewardAdsBridge 가 전투 화면 RewardAds 에 연결)</summary>
public sealed class AdMobRewardedAdService
{
    // 실제 광고 단위 ID 는 저장소에 없다 - 릴리스 빌드 동안만 Resources/AdUnits.txt 로 들어온다 (ReleaseBuilder).
    // 없거나 개발 빌드면 Google 테스트 광고.
    static string RewardedId
    {
        get
        {
            var file = Resources.Load<TextAsset>(DefenseBattle.View.AdUnitConfig.ResourceName);
            return DefenseBattle.View.AdUnitConfig.RewardedId(file != null ? file.text : null, Debug.isDebugBuild);
        }
    }

    public static AdMobRewardedAdService Shared { get; } = new AdMobRewardedAdService();

    RewardedAd m_ad;
    bool m_loading;
    bool m_showing;
    Action<bool> m_completion;
    bool m_rewardEarned;
    bool m_initialized;

    public bool IsReady => !AdsRemoved && !m_showing && m_ad != null && m_ad.CanShowAd();

    AdMobRewardedAdService()
    {
#if UNITY_ANDROID || UNITY_IOS
        // 광고 동의 확인(UMP)이 끝나고 광고를 요청해도 될 때만 초기화·첫 광고 요청
        AdConsentManager.WhenCanRequestAds(() => MobileAds.Initialize(status =>
        {
            if (status == null) return;
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                m_initialized = true;
                Load();
            });
        }));
#endif
    }

    public void Show(Action<bool> completed)
    {
        if (!IsReady)
        {
            completed?.Invoke(false);
            if (!AdsRemoved) Load();
            return;
        }

        m_showing = true;
        m_rewardEarned = false;
        m_completion = completed;
        RewardedAd ad = m_ad;
        m_ad = null;

        ad.OnAdFullScreenContentClosed += () =>
            MobileAdsEventExecutor.ExecuteInUpdate(() => Finish(ad, m_rewardEarned));
        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"AdMob rewarded ad failed to open: {error}");
            MobileAdsEventExecutor.ExecuteInUpdate(() => Finish(ad, false));
        };
        ad.Show(reward => m_rewardEarned = true);
    }

    void Load()
    {
        if (!m_initialized || AdsRemoved || m_loading || m_showing || m_ad != null) return;
        m_loading = true;
        RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() => CompleteLoad(ad, error));
        });
    }

    void CompleteLoad(RewardedAd ad, LoadAdError error)
    {
            m_loading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"AdMob rewarded ad failed to load: {error}");
                return;
            }

            if (AdsRemoved)
            {
                ad.Destroy();
                return;
            }

            m_ad = ad;
    }

    void Finish(RewardedAd ad, bool success)
    {
        if (!m_showing)
        {
            ad.Destroy();
            return;
        }
        ad.Destroy();
        m_showing = false;
        Action<bool> completion = m_completion;
        m_completion = null;
        completion?.Invoke(success);
        if (!AdsRemoved) Load();
    }

    /// <summary>광고 제거 패키지 (서버 경제 상태)</summary>
    static bool AdsRemoved => DefenseBattle.View.EconCache.State.AdRemoved;
}
