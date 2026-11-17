using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>스토어 결제 (5단계: Unity IAP 로 Google Play 결제 → 서버 RedeemPurchase 가 영수증 검증·지급)</summary>
    public interface IStore
    {
        /// <summary>스토어가 알려 준 현지 가격 글자 (모르면 null)</summary>
        string PriceOf(string productId);

        /// <summary>결제 → 서버 지급까지 (shop.ts purchase). 지급됐으면 상자 공개 목록(없으면 빈 목록), 취소·실패면 null</summary>
        Task<List<Reveal>> Buy(string productId);
    }
}
