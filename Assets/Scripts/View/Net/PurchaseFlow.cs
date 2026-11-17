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
        readonly List<IapOrder> m_unredeemed = new List<IapOrder>();
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
            IapOrder paid = TakeUnredeemed(productId);
            if (paid != null)
            {
                // 이 상품은 결제됐는데 서버에 못 닿았다 - 스토어는 '이미 가진 상품'으로 거절하니 그 주문을 지급한다
                RedeemWaiting(paid);
                return t;
            }
            try { m_backend.Buy(productId, m_accountTag()); }
            catch (Exception ex)
            {
                Debug.LogWarning("[IAP] 결제 시작 실패: " + ex.Message);
                Complete(null);
            }
            return t;
        }

        /// <summary>결제는 됐는데 서버에 못 닿은 주문을 다시 보낸다 (앱으로 돌아올 때). 지급되면 Restored</summary>
        public async Task RetryUnredeemed()
        {
            if (m_unredeemed.Count == 0) return;
            var orders = new List<IapOrder>(m_unredeemed);
            m_unredeemed.Clear();
            foreach (IapOrder o in orders)
            {
                List<Reveal> r = await Redeem(o);
                if (r != null) Restored?.Invoke(o.ProductId, r);
            }
        }

        IapOrder TakeUnredeemed(string productId)
        {
            for (int i = 0; i < m_unredeemed.Count; i++)
            {
                if (m_unredeemed[i].ProductId != productId) continue;
                IapOrder o = m_unredeemed[i];
                m_unredeemed.RemoveAt(i);
                return o;
            }
            return null;
        }

        async void RedeemWaiting(IapOrder order) => Complete(await Redeem(order));

        void OnFailed(string productId, bool cancelled)
        {
            if (m_wait == null) return;
            // 실패 id 가 다른 상품이면 그 주문 일. 그 밖(이 상품·null·"InvalidProduct"·구매 토큰)은 열려 있는 결제 창의 실패
            if (productId != null && productId != m_waitId && Products.Get(productId) != null) return;
            Complete(null);
        }

        async void OnPending(IapOrder order)
        {
            if (order == null) return;
            List<Reveal> result = await Redeem(order);
            if (m_wait != null && order.ProductId == m_waitId) Complete(result);
            else if (result != null) Restored?.Invoke(order.ProductId, result);
        }

        /// <summary>서버 지급 → 지급(또는 이미 지급)이면 주문 확인하고 공개 목록, 아니면 null. 서버에 못 닿으면 다시 보낼 목록에 둔다</summary>
        async Task<List<Reveal>> Redeem(IapOrder order)
        {
            string key = order.Receipt ?? order.ProductId;
            if (!m_redeeming.Add(key)) return null; // 같은 주문이 두 번 알려져도 서버엔 한 번
            try
            {
                if (!GoogleReceipt.TryParse(order.Receipt, out string json, out string signature))
                {
                    Debug.LogWarning("[IAP] 영수증을 읽을 수 없음: " + order.ProductId);
                    return null;
                }
                decimal? price = m_backend.LocalPrice(order.ProductId);
                PurchaseReply r = await m_redeem(new RedeemArgs
                {
                    ProductId = order.ProductId, PurchaseData = json, Signature = signature,
                    LocalCost = price.HasValue ? (int)Math.Round(price.Value) : 0, Currency = m_backend.Currency(order.ProductId),
                });
                if (r == null || !(r.Granted || r.AlreadyGranted))
                {
                    Debug.LogWarning("[IAP] 서버가 영수증을 받지 않음 - 주문을 확인하지 않는다: " + order.ProductId);
                    return null;
                }
                if (r.Econ != null) EconCache.Apply(r.Econ);
                m_backend.Confirm(order);
                return r.Reveals ?? new List<Reveal>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[IAP] 지급 요청 실패 (앱으로 돌아오거나 다시 누르면 다시): " + ex.Message);
                if (!m_unredeemed.Contains(order)) m_unredeemed.Add(order);
                return null;
            }
            finally { m_redeeming.Remove(key); }
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
