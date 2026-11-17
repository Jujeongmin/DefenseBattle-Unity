using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 결제 → 서버 지급 (shop.ts purchase · syncPurchases, spec 대응표 $onItemPurchased → Unity IAP + Cloud Code 검증).
    /// 스토어가 결제됐다고 알리면 영수증 원문·서명을 서버 RedeemPurchase 로 보내고, 서버가 지급(또는 이미 지급)했다고 할 때만
    /// 스토어 주문을 확인한다 - 검증에 실패한 주문은 확인하지 않아 Google 이 자동 환불한다. 같은 주문의 중복 지급은 서버가 막는다.
    /// 결제 화면 없이 온 주문(지난번에 앱이 꺼져 확인 못 한 것)도 같은 방식으로 지급하고 Restored 로 알린다.
    /// </summary>
    public sealed class PurchaseFlow : IStore
    {
        readonly IIapBackend m_backend;
        readonly Func<string> m_accountTag;
        readonly Func<RedeemArgs, Task<PurchaseReply>> m_redeem;
        readonly HashSet<string> m_redeeming = new HashSet<string>();
        TaskCompletionSource<List<Reveal>> m_wait;
        string m_waitId;

        /// <summary>결제 화면 없이 지급된 주문 (상품 id, 상자 공개) - "결제한 상품이 지급되었어요"</summary>
        public event Action<string, List<Reveal>> Restored;

        public bool Busy => m_wait != null;
        public bool Connected { get; private set; }

        /// <param name="accountTag">결제를 플레이어에 묶는 값 (PurchaseTag.ForPlayer(PlayerId))</param>
        /// <param name="redeem">서버 RedeemPurchase</param>
        public PurchaseFlow(IIapBackend backend, Func<string> accountTag, Func<RedeemArgs, Task<PurchaseReply>> redeem)
        {
            m_backend = backend;
            m_accountTag = accountTag;
            m_redeem = redeem;
            backend.Pending += OnPending;
            backend.Failed += OnFailed;
        }

        /// <summary>스토어 연결 (상품 전체 가격). 실패하면 가격은 기본 원화, 결제는 연결 안내</summary>
        public async Task<bool> Connect()
        {
            var ids = new List<string>();
            foreach (ProductDef p in Products.All) ids.Add(p.Id);
            try { Connected = await m_backend.Connect(ids); }
            catch (Exception ex)
            {
                Debug.LogWarning("[IAP] 스토어 연결 실패: " + ex.Message);
                Connected = false;
            }
            return Connected;
        }

        public string PriceOf(string productId) => Connected ? m_backend.PriceOf(productId) : null;

        public Task<List<Reveal>> Buy(string productId)
        {
            if (m_wait != null) return Task.FromResult<List<Reveal>>(null); // 결제 창은 한 번에 하나
            m_wait = new TaskCompletionSource<List<Reveal>>();
            m_waitId = productId;
            Task<List<Reveal>> t = m_wait.Task;
            try { m_backend.Buy(productId, m_accountTag()); }
            catch (Exception ex)
            {
                Debug.LogWarning("[IAP] 결제 시작 실패: " + ex.Message);
                Complete(null);
            }
            return t;
        }

        void OnFailed(string productId, bool cancelled)
        {
            if (m_wait != null && (productId == null || productId == m_waitId)) Complete(null);
        }

        async void OnPending(IapOrder order)
        {
            if (order == null) return;
            string key = order.Receipt ?? order.ProductId;
            if (!m_redeeming.Add(key)) return; // 같은 주문이 두 번 알려져도 서버엔 한 번
            List<Reveal> result = null;
            try
            {
                if (GoogleReceipt.TryParse(order.Receipt, out string json, out string signature))
                {
                    decimal? price = m_backend.LocalPrice(order.ProductId);
                    PurchaseReply r = await m_redeem(new RedeemArgs
                    {
                        ProductId = order.ProductId, PurchaseData = json, Signature = signature,
                        LocalCost = price.HasValue ? (int)Math.Round(price.Value) : 0, Currency = m_backend.Currency(order.ProductId),
                    });
                    if (r != null && (r.Granted || r.AlreadyGranted))
                    {
                        if (r.Econ != null) EconCache.Apply(r.Econ);
                        m_backend.Confirm(order);
                        result = r.Reveals ?? new List<Reveal>();
                    }
                    else Debug.LogWarning("[IAP] 서버가 영수증을 받지 않음 - 주문을 확인하지 않는다: " + order.ProductId);
                }
                else Debug.LogWarning("[IAP] 영수증을 읽을 수 없음: " + order.ProductId);
            }
            catch (Exception ex) { Debug.LogWarning("[IAP] 지급 요청 실패 (다음 실행에서 다시): " + ex.Message); }
            finally { m_redeeming.Remove(key); }

            if (m_wait != null && order.ProductId == m_waitId) Complete(result);
            else if (result != null) Restored?.Invoke(order.ProductId, result);
        }

        void Complete(List<Reveal> result)
        {
            TaskCompletionSource<List<Reveal>> w = m_wait;
            m_wait = null;
            m_waitId = null;
            w?.TrySetResult(result);
        }
    }
}
