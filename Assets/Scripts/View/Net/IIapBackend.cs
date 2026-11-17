using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DefenseBattle.View
{
    /// <summary>스토어 SDK 연결부 (Unity IAP). 결제 흐름(PurchaseFlow)은 이 모양만 안다 - 테스트는 가짜로 바꾼다</summary>
    public interface IIapBackend
    {
        /// <summary>결제됨 (방금 결제 / 지난번에 확인 못 한 주문)</summary>
        event Action<IapOrder> Pending;
        /// <summary>결제 실패·취소 (상품 id, 사용자가 취소했나)</summary>
        event Action<string, bool> Failed;

        /// <summary>스토어 연결 + 상품 가격 불러오기. 성공하면 true</summary>
        Task<bool> Connect(IReadOnlyList<string> productIds);
        /// <summary>현지 가격 글자 (모르면 null)</summary>
        string PriceOf(string productId);
        decimal? LocalPrice(string productId);
        string Currency(string productId);
        /// <summary>결제 창 열기. accountTag = Google obfuscatedAccountId (PurchaseTag)</summary>
        void Buy(string productId, string accountTag);
        /// <summary>지급이 끝난 주문 확인 (소모 - 다시 살 수 있게)</summary>
        void Confirm(IapOrder order);
    }
}
