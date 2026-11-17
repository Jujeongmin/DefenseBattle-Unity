using System;

namespace DefenseBattle.View
{
    /// <summary>
    /// 광고 개인정보 옵션 (Google UMP). 동의가 필요한 지역(EEA·영국 등)에서는 동의를 다시 고를 수 있는 창을 설정에 둬야 한다.
    /// 기기 쪽(AdConsentManager)이 채운다 - 에디터·필요 없는 지역에선 비어 있어 설정에 줄이 없다.
    /// </summary>
    public static class AdPrivacy
    {
        /// <summary>개인정보 옵션 창을 보여 줘야 하는 지역인가</summary>
        public static Func<bool> Required;
        /// <summary>UMP 개인정보 옵션 창 열기</summary>
        public static Action Show;

        public static bool Available => Show != null && Required != null && Required();
    }
}
