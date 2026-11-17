using System;
using System.Collections.Generic;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 지금 쓰는 스토어. 스토어에 연결되기 전(또는 실패)엔 결제 버튼이 안내만 하고, 가격은 상품 기본 원화 가격으로 보여 준다.
    /// 연결은 PlatformBridge(기기 SDK)가 UGS 로그인 뒤에 한다.
    /// </summary>
    public static class Store
    {
        public static IStore Service;

        public static bool Ready => Service != null;

        /// <summary>스토어 연결을 시도했지만 실패함 (오프라인·결제 불가 기기)</summary>
        public static bool ConnectFailed;

        /// <summary>결제 화면 없이 지급된 주문 (지난번에 앱이 꺼져 확인 못 한 결제) - 로비가 안내·상자 공개</summary>
        public static event Action<string, List<Reveal>> Restored;

        public static void RaiseRestored(string productId, List<Reveal> reveals) => Restored?.Invoke(productId, reveals);

        /// <summary>버튼에 쓸 가격 (스토어 현지 가격 우선)</summary>
        public static string Price(ProductDef p) => Service?.PriceOf(p.Id) ?? "₩" + Ui.Num(p.Krw);
    }
}
