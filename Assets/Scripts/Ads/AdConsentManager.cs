using System;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Common;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

/// <summary>
/// 앱 시작 시 개인정보 동의를 처리합니다.
/// - iOS: ATT(App Tracking Transparency) 권한 요청 (iOS 14.5+ 필수)
/// - GDPR(유럽): Google UMP 동의 폼 표시
/// 광고(MobileAds 초기화·광고 요청)는 동의 확인이 끝나고 CanRequestAds 일 때만 시작한다 - WhenCanRequestAds.
/// 동의가 필요한 지역이면 설정의 '광고 개인정보 설정'으로 다시 고를 수 있다 (AdPrivacy).
/// </summary>
public static class AdConsentManager
{
    static bool s_started;
    static bool s_canRequest;
    static Action s_waiting;

    /// <summary>광고를 요청해도 될 때 한 번 부른다 (이미 됐으면 바로)</summary>
    public static void WhenCanRequestAds(Action a)
    {
        if (s_canRequest) a();
        else s_waiting += a;
    }

    static void Ready()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (s_canRequest || !ConsentInformation.CanRequestAds()) return;
        s_canRequest = true;
        Action w = s_waiting;
        s_waiting = null;
        w?.Invoke();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (s_started) return;
        s_started = true;

#if UNITY_ANDROID || UNITY_IOS
        // 전체이용가 게임에 맞게 성인용 광고 차단: T(Teen) 이하 등급의 광고만 허용
        MobileAds.SetRequestConfiguration(new RequestConfiguration
        {
            MaxAdContentRating = MaxAdContentRating.T
        });
#endif

#if UNITY_IOS
        RequestAppTrackingTransparency();
#endif
        GatherConsent();
    }

#if UNITY_IOS
    static void RequestAppTrackingTransparency()
    {
        if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus()
            == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            ATTrackingStatusBinding.RequestAuthorizationTracking();
        }
    }
#endif

    static void GatherConsent()
    {
#if UNITY_ANDROID || UNITY_IOS
        ConsentRequestParameters request = new ConsentRequestParameters();

        // ── 개발 중 동의 폼 강제 테스트가 필요하면 아래 주석을 해제하세요 ──
        // request.ConsentDebugSettings = new ConsentDebugSettings
        // {
        //     DebugGeography = DebugGeography.EEA,
        //     TestDeviceHashedIds = new System.Collections.Generic.List<string> { "여기에-테스트-기기-해시-ID" }
        // };

        DefenseBattle.View.AdPrivacy.Required = () => ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
        DefenseBattle.View.AdPrivacy.Show = () => ConsentForm.ShowPrivacyOptionsForm(err =>
        {
            if (err != null) Debug.LogWarning($"[Consent] 개인정보 옵션 창 실패: {err.Message}");
        });

        // 지난 실행에서 받은 동의가 있으면 바로 광고를 시작할 수 있다 (갱신은 계속 진행)
        MobileAdsEventExecutor.ExecuteInUpdate(Ready);
        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning($"[Consent] 동의 정보 갱신 실패: {updateError.Message}");
                MobileAdsEventExecutor.ExecuteInUpdate(Ready);
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                    Debug.LogWarning($"[Consent] 동의 폼 표시 실패: {formError.Message}");
                MobileAdsEventExecutor.ExecuteInUpdate(Ready); // 동의를 받았거나 필요 없으면 광고 시작
            });
        });
#endif
    }
}
