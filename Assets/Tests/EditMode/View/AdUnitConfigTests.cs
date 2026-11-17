using NUnit.Framework;

namespace DefenseBattle.View.Tests
{
    /// <summary>광고 단위 ID 설정 (릴리스 빌드 때만 Resources/AdUnits 로 들어온다 - 저장소에는 없음)</summary>
    public class AdUnitConfigTests
    {
        [Test]
        public void NoFile_UsesGoogleTestId()
        {
            Assert.AreEqual(AdUnitConfig.TestRewardedAndroid, AdUnitConfig.RewardedId(null, false));
            Assert.AreEqual(AdUnitConfig.TestRewardedAndroid, AdUnitConfig.RewardedId("", false));
        }

        [Test]
        public void File_GivesRealId()
        {
            const string text = "# 릴리스 빌드가 넣는 파일\nrewarded = ca-app-pub-1111111111111111/2222222222\n";
            Assert.AreEqual("ca-app-pub-1111111111111111/2222222222", AdUnitConfig.RewardedId(text, false));
        }

        [Test]
        public void DevelopmentBuild_AlwaysTestId()
        {
            Assert.AreEqual(AdUnitConfig.TestRewardedAndroid, AdUnitConfig.RewardedId("rewarded=ca-app-pub-1/2", true));
        }

        [Test]
        public void MalformedValue_FallsBackToTestId()
        {
            Assert.AreEqual(AdUnitConfig.TestRewardedAndroid, AdUnitConfig.RewardedId("rewarded=\nother=x", false));
            Assert.AreEqual(AdUnitConfig.TestRewardedAndroid, AdUnitConfig.RewardedId("rewarded=not-an-id", false));
        }

        [Test]
        public void Format_RoundTrips()
        {
            string text = AdUnitConfig.Format("ca-app-pub-9/8");
            Assert.AreEqual("ca-app-pub-9/8", AdUnitConfig.RewardedId(text, false));
        }
    }
}
