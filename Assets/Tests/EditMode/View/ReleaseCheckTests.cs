using NUnit.Framework;

namespace DefenseBattle.View.Tests
{
    /// <summary>릴리스 빌드 전 검사 (테스트 광고·예전 앱 설정으로 출시되지 않게)</summary>
    public class ReleaseCheckTests
    {
        const string Pkg = "com.jeonja.defensebattle";
        const string AppId = "ca-app-pub-1234567890123456~1111111111";
        const string Unit = "ca-app-pub-1234567890123456/2222222222";

        static ReleaseCheck.Input Good() => new ReleaseCheck.Input
        {
            KeystoreExists = true, KeyAlias = "upload", KeystorePass = "pw",
            AdMobAppId = AppId, RewardedId = Unit, AdsSettingsFound = true,
            PackageName = Pkg, GpgsSettingsText = "proj.AppId=987654321\nand.BundleId=" + Pkg + "\n",
        };

        [Test]
        public void AllGood_NothingMissing()
        {
            CollectionAssert.IsEmpty(ReleaseCheck.Missing(Good()));
        }

        [Test]
        public void AdsSettingsFieldNotFound_Blocks()
        {
            // 플러그인이 바뀌어 앱 ID 칸을 못 찾으면 테스트 앱 ID 로 나가므로 멈춘다
            ReleaseCheck.Input i = Good();
            i.AdsSettingsFound = false;
            Assert.IsNotEmpty(ReleaseCheck.Missing(i));
        }

        [Test]
        public void OldPlayGamesSettings_Blocks()
        {
            ReleaseCheck.Input i = Good();
            i.GpgsSettingsText = "proj.AppId=640387505052\nand.BundleId=com.junja.randomdefense\n";
            Assert.IsNotEmpty(ReleaseCheck.Missing(i));
        }

        [Test]
        public void MissingPlayGamesSettings_Blocks()
        {
            ReleaseCheck.Input i = Good();
            i.GpgsSettingsText = null;
            Assert.IsNotEmpty(ReleaseCheck.Missing(i));
        }

        [Test]
        public void TestAdIds_Block()
        {
            ReleaseCheck.Input i = Good();
            i.AdMobAppId = AdUnitConfig.TestAppIdAndroid;
            Assert.IsNotEmpty(ReleaseCheck.Missing(i));
            i = Good();
            i.RewardedId = AdUnitConfig.TestRewardedAndroid;
            Assert.IsNotEmpty(ReleaseCheck.Missing(i));
        }

        [Test]
        public void IdsFromDifferentAdMobAccounts_Block()
        {
            ReleaseCheck.Input i = Good();
            i.RewardedId = "ca-app-pub-9999999999999999/2222222222";
            Assert.IsNotEmpty(ReleaseCheck.Missing(i));
        }

        [Test]
        public void KeystoreValues_Required()
        {
            ReleaseCheck.Input i = Good();
            i.KeystoreExists = false;
            i.KeystorePass = null;
            Assert.AreEqual(2, ReleaseCheck.Missing(i).Count);
        }
    }
}
