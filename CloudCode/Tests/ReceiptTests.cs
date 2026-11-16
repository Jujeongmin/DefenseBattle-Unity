using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DefenseBattle.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DefenseBattle.Server.Tests
{
    /// <summary>Google Play 영수증 서명 검증 (Economy 대신). 테스트용 RSA 키로 Play 처럼 서명한다</summary>
    public class ReceiptTests
    {
        RSA m_rsa;
        string m_publicKey;

        [SetUp]
        public void SetUp()
        {
            m_rsa = RSA.Create(2048);
            m_publicKey = Convert.ToBase64String(m_rsa.ExportSubjectPublicKeyInfo());
        }

        [TearDown]
        public void TearDown() => m_rsa.Dispose();

        string Data(string product = "crystal_550", string player = "p1", string package = PurchaseTag.PackageName, int state = 0, string order = "GPA.1")
            => new JObject
            {
                ["orderId"] = order,
                ["packageName"] = package,
                ["productId"] = product,
                ["purchaseTime"] = 1794800000000L,
                ["purchaseState"] = state,
                ["purchaseToken"] = "tok-" + order,
                ["obfuscatedAccountId"] = PurchaseTag.ForPlayer(player),
            }.ToString(Newtonsoft.Json.Formatting.None);

        string Sign(string data) => Convert.ToBase64String(m_rsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));

        GooglePlayReceiptVerifier Verifier(string key = null) => new GooglePlayReceiptVerifier(PurchaseTag.PackageName, () => Task.FromResult(key ?? m_publicKey));

        [Test]
        public async Task ValidReceipt_PassesWithOrderId()
        {
            string d = Data();
            ReceiptCheck c = await Verifier().Verify("p1", "crystal_550", d, Sign(d), 6500, "KRW");
            Assert.IsTrue(c.Valid);
            Assert.AreEqual("GPA.1", c.OrderId);
        }

        [Test]
        public async Task TamperedData_Fails()
        {
            string d = Data();
            string sig = Sign(d);
            string forged = d.Replace("crystal_550", "crystal_15000");
            Assert.IsFalse((await Verifier().Verify("p1", "crystal_15000", forged, sig, 0, "KRW")).Valid);
        }

        [Test]
        public async Task OtherPlayersReceipt_Fails()
        {
            string d = Data(player: "p1");
            Assert.IsFalse((await Verifier().Verify("p2", "crystal_550", d, Sign(d), 0, "KRW")).Valid);
        }

        [Test]
        public async Task WrongPackageOrProductOrPending_Fails()
        {
            string otherApp = Data(package: "com.other.app");
            Assert.IsFalse((await Verifier().Verify("p1", "crystal_550", otherApp, Sign(otherApp), 0, "KRW")).Valid);
            string d = Data();
            Assert.IsFalse((await Verifier().Verify("p1", "crystal_100", d, Sign(d), 0, "KRW")).Valid);
            string pending = Data(state: 2);
            Assert.IsFalse((await Verifier().Verify("p1", "crystal_550", pending, Sign(pending), 0, "KRW")).Valid);
        }

        [Test]
        public async Task MissingOrBrokenKeyOrSignature_FailsWithoutThrowing()
        {
            string d = Data();
            Assert.IsFalse((await Verifier("").Verify("p1", "crystal_550", d, Sign(d), 0, "KRW")).Valid);
            Assert.IsFalse((await Verifier("not base64!").Verify("p1", "crystal_550", d, Sign(d), 0, "KRW")).Valid);
            Assert.IsFalse((await Verifier().Verify("p1", "crystal_550", d, "%%%", 0, "KRW")).Valid);
            Assert.IsFalse((await Verifier().Verify("p1", "crystal_550", d, "", 0, "KRW")).Valid);
            using (RSA other = RSA.Create(2048))
            {
                string otherKey = Convert.ToBase64String(other.ExportSubjectPublicKeyInfo());
                Assert.IsFalse((await Verifier(otherKey).Verify("p1", "crystal_550", d, Sign(d), 0, "KRW")).Valid);
            }
        }

        [Test]
        public async Task SameOrder_GrantedOnceThroughServer()
        {
            var rig = new Rig();
            var server = new GameServer(rig.Store, rig.Clock, rig.Push, rig.Rank, Verifier());
            string d = Data(product: "crystal_100");
            PurchaseReply first = await server.RedeemPurchase("p1", "crystal_100", d, Sign(d), 1200, "KRW");
            PurchaseReply again = await server.RedeemPurchase("p1", "crystal_100", d, Sign(d), 1200, "KRW");
            Assert.IsTrue(first.Granted);
            Assert.IsFalse(again.Granted);
            Assert.AreEqual(100, again.Econ.Crystals);
        }

        [Test]
        public void PurchaseTag_IsStableHexOfLength64()
        {
            Assert.AreEqual(64, PurchaseTag.ForPlayer("abc").Length);
            Assert.AreEqual(PurchaseTag.ForPlayer("abc"), PurchaseTag.ForPlayer("abc"));
            Assert.AreNotEqual(PurchaseTag.ForPlayer("abc"), PurchaseTag.ForPlayer("abd"));
            // SHA-256("abc") 알려진 값
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", PurchaseTag.ForPlayer("abc"));
        }
    }
}
