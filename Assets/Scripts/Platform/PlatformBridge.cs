using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using DefenseBattle.Services;
using DefenseBattle.View;
using UnityEngine;

/// <summary>
/// 스토어 SDK 를 게임에 연결한다 (결제 흐름 PurchaseFlow + Unity IAP). 씬을 바꾸지 않도록 첫 씬이 열릴 때 스스로 만들어진다.
/// UGS 로그인이 끝나면 스토어에 연결하고, 연결되면 Store.Service 로 쓴다 (그 전·실패 시 결제 버튼은 연결 안내).
/// 지난번에 지급·확인하지 못한 주문은 연결 직후 서버로 보내 지급하고 Store.Restored 로 알린다.
/// </summary>
public sealed class PlatformBridge : MonoBehaviour
{
    PurchaseFlow m_flow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("PlatformBridge");
        DontDestroyOnLoad(go);
        go.AddComponent<PlatformBridge>();
    }

    async void Start()
    {
        m_flow = new PurchaseFlow(new UnityIapBackend(), () => PurchaseTag.ForPlayer(UgsSession.PlayerId), Redeem);
        m_flow.Restored += (id, reveals) => Store.RaiseRestored(id, reveals);
        while (UgsSession.PlayerId == null)
        {
            if (this == null) return;
            await Task.Delay(500);
        }
        if (this == null) return;
        if (await m_flow.Connect()) Store.Service = m_flow;
        else Store.ConnectFailed = true;
    }

    static Task<PurchaseReply> Redeem(RedeemArgs a) => CloudClient.Call<PurchaseReply>(CloudApi.RedeemPurchase, new Dictionary<string, object>
    {
        { "productId", a.ProductId }, { "purchaseData", a.PurchaseData }, { "signature", a.Signature },
        { "localCost", a.LocalCost }, { "localCurrency", a.Currency ?? "" },
    });
}
