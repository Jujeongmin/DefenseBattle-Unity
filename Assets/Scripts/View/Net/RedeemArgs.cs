namespace DefenseBattle.View
{
    /// <summary>서버 RedeemPurchase 인자 (productId, purchaseData, signature, localCost, localCurrency)</summary>
    public sealed class RedeemArgs
    {
        public string ProductId;
        /// <summary>Google 구매 원문 JSON (서명 대상 - 그대로 보낸다)</summary>
        public string PurchaseData;
        public string Signature;
        public int LocalCost;
        public string Currency;
    }
}
