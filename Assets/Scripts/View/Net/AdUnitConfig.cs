using System;

namespace DefenseBattle.View
{
    /// <summary>
    /// AdMob 보상형 광고 단위 ID. 실제 ID 는 저장소에 두지 않고 릴리스 빌드(ReleaseBuilder) 동안만 Resources/AdUnits.txt 로 넣는다.
    /// 파일이 없거나(에디터·테스트 빌드) 개발 빌드면 Google 공식 테스트 광고를 쓴다.
    /// 파일 형식: 한 줄에 "rewarded = ca-app-pub-…/…" (# 줄은 주석).
    /// </summary>
    public static class AdUnitConfig
    {
        /// <summary>Resources 안 파일 이름 (확장자 없이)</summary>
        public const string ResourceName = "AdUnits";
        /// <summary>Google 공식 테스트 보상형 광고 (Android)</summary>
        public const string TestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
        /// <summary>Google 공식 테스트 AdMob 앱 ID (Android)</summary>
        public const string TestAppIdAndroid = "ca-app-pub-3940256099942544~3347511713";

        const string Key = "rewarded";

        public static string RewardedId(string fileText, bool developmentBuild)
        {
            if (developmentBuild || string.IsNullOrEmpty(fileText)) return TestRewardedAndroid;
            foreach (string raw in fileText.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq < 0 || line.Substring(0, eq).Trim() != Key) continue;
                string v = line.Substring(eq + 1).Trim();
                return IsUnitId(v) ? v : TestRewardedAndroid;
            }
            return TestRewardedAndroid;
        }

        /// <summary>릴리스 빌드가 쓰는 파일 내용</summary>
        public static string Format(string rewardedId) => "# ReleaseBuilder 가 빌드 동안만 넣는 파일 - 커밋하지 않는다\n" + Key + " = " + rewardedId + "\n";

        /// <summary>광고 단위 ID 모양 (ca-app-pub-숫자/숫자)</summary>
        public static bool IsUnitId(string v) => v != null && v.StartsWith("ca-app-pub-", StringComparison.Ordinal) && v.IndexOf('/') > 11;

        /// <summary>AdMob 앱 ID 모양 (ca-app-pub-숫자~숫자)</summary>
        public static bool IsAppId(string v) => v != null && v.StartsWith("ca-app-pub-", StringComparison.Ordinal) && v.IndexOf('~') > 11;
    }
}
