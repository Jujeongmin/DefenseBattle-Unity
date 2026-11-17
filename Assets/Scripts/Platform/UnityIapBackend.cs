using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.View;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// Unity IAP 5 (StoreController) 연결부. 상품은 모두 소모형으로 등록한다 - 한 번만 파는 상품(스타터·광고 제거·유닛 패키지)은
/// 서버가 OneTime 기록으로 막고, 스토어 쪽은 지급 뒤 확인(소모)해 같은 상품을 다시 살 수 있게 둔다.
/// 결제는 Google obfuscatedAccountId 에 플레이어 표시를 넣어 연다 (서버가 남의 영수증 재사용을 막는 값).
/// </summary>
public sealed class UnityIapBackend : IIapBackend
{
    const float FetchTimeout = 15f;

    StoreController m_store;
    readonly Dictionary<string, Product> m_products = new Dictionary<string, Product>();

    public event Action<IapOrder> Pending;
    public event Action<string, bool> Failed;

    public async Task<bool> Connect(IReadOnlyList<string> productIds)
    {
        if (m_store == null)
        {
            m_store = UnityIAPServices.StoreController();
            m_store.OnPurchasePending += OnPending;
            m_store.OnPurchaseFailed += OnFailed;
            // 결제 보류 (현금 결제 등 승인 대기): 지금 창은 닫고, 승인되면 OnPurchasePending 으로 와서 Restored 로 지급된다
            m_store.OnPurchaseDeferred += o => Failed?.Invoke(FirstId(o), false);
            m_store.OnStoreConnected += () => Debug.Log("[IAP] 스토어 연결됨");
            m_store.OnStoreDisconnected += f => Debug.LogWarning("[IAP] 스토어 연결 끊김: " + f.Message);
            m_store.OnPurchasesFetched += _ => { };
            m_store.OnPurchasesFetchFailed += f => Debug.LogWarning("[IAP] 지난 결제 불러오기 실패: " + f.Message);
        }
        var fetched = new TaskCompletionSource<bool>();
        m_store.OnProductsFetched += list =>
        {
            foreach (Product p in list) m_products[p.definition.id] = p;
            fetched.TrySetResult(true);
        };
        m_store.OnProductsFetchFailed += f =>
        {
            Debug.LogWarning("[IAP] 상품 불러오기 실패: " + f.FailureReason);
            fetched.TrySetResult(m_products.Count > 0);
        };
        await m_store.Connect();
        var defs = new List<ProductDefinition>(productIds.Count);
        foreach (string id in productIds) defs.Add(new ProductDefinition(id, ProductType.Consumable));
        m_store.FetchProducts(defs);
        Task first = await Task.WhenAny(fetched.Task, Task.Delay(TimeSpan.FromSeconds(FetchTimeout)));
        bool ok = first == fetched.Task && fetched.Task.Result;
        // 지난번에 지급·확인하지 못한 주문을 다시 받는다 (OnPurchasePending 으로 온다)
        m_store.ProcessPendingOrdersOnPurchasesFetched(true);
        m_store.FetchPurchases();
        return ok;
    }

    /// <summary>에디터 가짜 스토어는 $0.01 을 주므로 에디터에선 상품 기본 원화 가격을 쓴다 (null)</summary>
    public string PriceOf(string productId)
        => Application.isEditor ? null : m_products.TryGetValue(productId, out Product p) && p.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString) ? p.metadata.localizedPriceString : null;

    public decimal? LocalPrice(string productId)
        => m_products.TryGetValue(productId, out Product p) && p.metadata != null ? p.metadata.localizedPrice : (decimal?)null;

    public string Currency(string productId)
        => m_products.TryGetValue(productId, out Product p) && p.metadata != null ? p.metadata.isoCurrencyCode : null;

    public void Buy(string productId, string accountTag)
    {
        if (m_store == null) throw new InvalidOperationException("store not connected");
        if (!string.IsNullOrEmpty(accountTag)) m_store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(accountTag);
        m_store.PurchaseProduct(productId);
    }

    public void Confirm(IapOrder order)
    {
        if (order?.Native is PendingOrder p) m_store.ConfirmPurchase(p);
    }

    static string FirstId(Order o)
    {
        IReadOnlyList<CartItem> items = o?.CartOrdered?.Items();
        return items != null && items.Count > 0 ? items[0].Product.definition.id : null;
    }

    void OnPending(PendingOrder o) => Pending?.Invoke(new IapOrder { ProductId = FirstId(o), Receipt = o.Info?.Receipt, Native = o });

    void OnFailed(FailedOrder o)
    {
        Debug.Log("[IAP] 결제 안 됨: " + o.FailureReason + " " + o.Details);
        Failed?.Invoke(FirstId(o), o.FailureReason == PurchaseFailureReason.UserCancelled);
    }
}
