using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 지금 쓰는 스토어. 5단계(IAP)가 Service 를 채우기 전에는 결제 버튼이 '준비 중' 안내만 하고,
    /// 가격은 상품 기본 원화 가격으로 보여 준다.
    /// </summary>
    public static class Store
    {
        public static IStore Service;

        public static bool Ready => Service != null;

        /// <summary>버튼에 쓸 가격 (스토어 현지 가격 우선)</summary>
        public static string Price(ProductDef p) => Service?.PriceOf(p.Id) ?? "₩" + Ui.Num(p.Krw);
    }
}
