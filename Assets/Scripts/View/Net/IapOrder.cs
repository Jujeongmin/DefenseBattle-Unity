namespace DefenseBattle.View
{
    /// <summary>스토어가 결제됐다고 알린 주문 (아직 확인 전)</summary>
    public sealed class IapOrder
    {
        public string ProductId;
        /// <summary>Unity 통합 영수증 JSON (GoogleReceipt.TryParse 로 원문·서명을 꺼낸다)</summary>
        public string Receipt;
        /// <summary>SDK 주문 객체 (확인할 때 연결부가 쓴다)</summary>
        public object Native;
    }
}
