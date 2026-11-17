using System;
using System.Collections.Generic;

namespace DefenseBattle.View
{
    /// <summary>
    /// 릴리스 빌드 전 검사 (ReleaseBuilder 가 쓴다). 빠진 값이나 출시하면 안 되는 설정을 사람이 읽을 문장으로 돌려준다 - 비어 있으면 빌드 가능.
    /// 테스트 광고 ID, 광고 설정 칸을 못 찾음(플러그인 변경), 예전 앱(랜덤 디펜스)의 Play 게임즈 설정, 서로 다른 AdMob 계정의 ID 를 막는다.
    /// </summary>
    public static class ReleaseCheck
    {
        public sealed class Input
        {
            public bool KeystoreExists;
            public string KeyAlias;
            public string KeystorePass;
            public string AdMobAppId;
            public string RewardedId;
            /// <summary>GoogleMobileAdsSettings 의 Android 앱 ID 칸을 찾았나</summary>
            public bool AdsSettingsFound;
            /// <summary>Player Settings 패키지 이름</summary>
            public string PackageName;
            /// <summary>ProjectSettings/GooglePlayGameSettings.txt 내용 (없으면 null)</summary>
            public string GpgsSettingsText;
        }

        public static List<string> Missing(Input i)
        {
            var miss = new List<string>();
            if (!i.KeystoreExists) miss.Add("keystorePath (파일이 있어야 함)");
            if (string.IsNullOrEmpty(i.KeyAlias)) miss.Add("keyAlias");
            if (string.IsNullOrEmpty(i.KeystorePass)) miss.Add("환경변수 DB_KEYSTORE_PASS");
            if (!AdUnitConfig.IsAppId(i.AdMobAppId)) miss.Add("admobAppId (ca-app-pub-…~…)");
            if (!AdUnitConfig.IsUnitId(i.RewardedId)) miss.Add("admobRewardedId (ca-app-pub-…/…)");
            if (i.AdMobAppId == AdUnitConfig.TestAppIdAndroid || i.RewardedId == AdUnitConfig.TestRewardedAndroid)
                miss.Add("실제 AdMob ID (지금 값은 Google 테스트 ID)");
            else if (AdUnitConfig.IsAppId(i.AdMobAppId) && AdUnitConfig.IsUnitId(i.RewardedId) && Publisher(i.AdMobAppId, '~') != Publisher(i.RewardedId, '/'))
                miss.Add("AdMob 앱 ID 와 광고 단위 ID 가 다른 계정 것 (ca-app-pub-숫자 부분이 같아야 함)");
            if (!i.AdsSettingsFound) miss.Add("GoogleMobileAdsSettings 의 Android 앱 ID 칸을 찾지 못함 (플러그인 변경 확인)");

            string bundle = SettingValue(i.GpgsSettingsText, "and.BundleId");
            if (bundle == null) miss.Add("Play 게임즈 설정 (Window → Google Play Games → Setup)");
            else if (bundle != i.PackageName) miss.Add("Play 게임즈 설정이 다른 앱 것 (" + bundle + ") - 새 앱 리소스로 Setup 을 다시 해야 함");
            return miss;
        }

        static string Publisher(string id, char sep)
        {
            int at = id.IndexOf(sep);
            return at > 0 ? id.Substring(0, at) : id;
        }

        static string SettingValue(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(key + "=", StringComparison.Ordinal)) return line.Substring(key.Length + 1).Trim();
            }
            return null;
        }
    }
}
