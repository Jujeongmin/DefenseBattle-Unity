using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DefenseBattle.View.Tests
{
    /// <summary>결제 흐름 (shop.ts purchase · syncPurchases): 서버 지급을 확인한 뒤에만 스토어 주문을 확인한다</summary>
    public class PurchaseFlowTests
    {
        sealed class FakeBackend : IIapBackend
        {
            public event Action<IapOrder> Pending;
            public event Action<string, bool> Failed;
            public readonly List<IapOrder> Confirmed = new List<IapOrder>();
            public readonly List<(string id, string tag)> Bought = new List<(string, string)>();
            public bool Connected = true;
            public Task<bool> Connect(IReadOnlyList<string> ids) => Task.FromResult(Connected);
            public string PriceOf(string id) => "₩9,999";
            public decimal? LocalPrice(string id) => 9999m;
            public string Currency(string id) => "KRW";
            public void Buy(string id, string tag) => Bought.Add((id, tag));
            public void Confirm(IapOrder o) => Confirmed.Add(o);
            public void Pend(string id) => Pending?.Invoke(new IapOrder { ProductId = id, Receipt = Receipt("{\"orderId\":\"o1\"}", "sig") });
            public void Fail(string id, bool cancelled) => Failed?.Invoke(id, cancelled);
        }

        static string Receipt(string json, string sig)
        {
            string payload = Newtonsoft.Json.JsonConvert.SerializeObject(new { json, signature = sig });
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { Store = "GooglePlay", TransactionID = "t", Payload = payload });
        }

        static PurchaseReply Granted() => new PurchaseReply { Granted = true, Reveals = new List<Reveal> { new Reveal { Title = "x" } } };

        static IEnumerator Wait(Task t)
        {
            for (int i = 0; i < 20 && !t.IsCompleted; i++) yield return null;
        }

        [Test]
        public void GoogleReceipt_ParsesPayload()
        {
            Assert.IsTrue(GoogleReceipt.TryParse(Receipt("{\"a\":1}", "SIG"), out string json, out string sig));
            Assert.AreEqual("{\"a\":1}", json);
            Assert.AreEqual("SIG", sig);
            Assert.IsFalse(GoogleReceipt.TryParse("not json", out _, out _));
            Assert.IsFalse(GoogleReceipt.TryParse(null, out _, out _));
        }

        [UnityTest]
        public IEnumerator Buy_GrantedConfirmsAndReturnsReveals()
        {
            var be = new FakeBackend();
            RedeemArgs got = null;
            var flow = new PurchaseFlow(be, () => "tag1", a => { got = a; return Task.FromResult(Granted()); });
            Task<List<Reveal>> buy = flow.Buy("crystal_100");
            Assert.AreEqual(("crystal_100", "tag1"), be.Bought[0], "결제를 플레이어 표시와 함께 연다");
            be.Pend("crystal_100");
            yield return Wait(buy);
            Assert.IsNotNull(buy.Result);
            Assert.AreEqual(1, buy.Result.Count);
            Assert.AreEqual(1, be.Confirmed.Count);
            Assert.AreEqual("{\"orderId\":\"o1\"}", got.PurchaseData);
            Assert.AreEqual("sig", got.Signature);
            Assert.AreEqual(9999, got.LocalCost);
            Assert.AreEqual("KRW", got.Currency);
        }

        [UnityTest]
        public IEnumerator Buy_RejectedDoesNotConfirm()
        {
            var be = new FakeBackend();
            var flow = new PurchaseFlow(be, () => "tag1", a => Task.FromResult(new PurchaseReply { Granted = false }));
            Task<List<Reveal>> buy = flow.Buy("crystal_100");
            be.Pend("crystal_100");
            yield return Wait(buy);
            Assert.IsNull(buy.Result, "검증 실패 → 지급 없음");
            Assert.AreEqual(0, be.Confirmed.Count, "확인하지 않아 스토어가 자동 환불");
        }

        [UnityTest]
        public IEnumerator Buy_AlreadyGrantedConfirms()
        {
            var be = new FakeBackend();
            var flow = new PurchaseFlow(be, () => "tag1", a => Task.FromResult(new PurchaseReply { Granted = false, AlreadyGranted = true }));
            Task<List<Reveal>> buy = flow.Buy("crystal_100");
            be.Pend("crystal_100");
            yield return Wait(buy);
            Assert.IsNotNull(buy.Result);
            Assert.AreEqual(1, be.Confirmed.Count);
        }

        [UnityTest]
        public IEnumerator Buy_CancelledReturnsNull()
        {
            var be = new FakeBackend();
            var flow = new PurchaseFlow(be, () => "tag1", a => Task.FromResult(Granted()));
            Task<List<Reveal>> buy = flow.Buy("crystal_100");
            be.Fail("crystal_100", true);
            yield return Wait(buy);
            Assert.IsNull(buy.Result);
            Assert.IsFalse(flow.Busy);
        }

        [Test]
        public void Buy_WhileBusy_DoesNotOpenSecondPurchase()
        {
            var be = new FakeBackend();
            var flow = new PurchaseFlow(be, () => "tag1", a => Task.FromResult(Granted()));
            flow.Buy("crystal_100");
            Task<List<Reveal>> second = flow.Buy("crystal_550");
            Assert.AreEqual(1, be.Bought.Count);
            Assert.IsTrue(second.IsCompleted);
            Assert.IsNull(second.Result);
        }

        [UnityTest]
        public IEnumerator PendingOnStart_RedeemedAndRestoredRaised()
        {
            // 결제 도중 앱이 꺼졌던 주문: 다음 실행에서 스토어가 다시 알려 주면 지급·확인하고 알린다
            var be = new FakeBackend();
            var flow = new PurchaseFlow(be, () => "tag1", a => Task.FromResult(Granted()));
            var restored = new List<string>();
            var reveals = new List<Reveal>();
            flow.Restored += (id, r) => { restored.Add(id); reveals.AddRange(r); };
            be.Pend("starter_pack");
            for (int i = 0; i < 20 && restored.Count == 0; i++) yield return null;
            CollectionAssert.AreEqual(new[] { "starter_pack" }, restored);
            Assert.AreEqual(1, reveals.Count);
            Assert.AreEqual(1, be.Confirmed.Count);
        }
    }
}
