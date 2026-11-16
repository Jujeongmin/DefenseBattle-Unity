namespace DefenseBattle.View
{
    /// <summary>결과 창 버튼이 하는 일</summary>
    public enum ResultAction
    {
        Revive,
        GiveUp,
        /// <summary>같은 판 바로 다시</summary>
        Retry,
        Lobby,
        Close,
        /// <summary>광고 보고 보상 2배</summary>
        Double,
        /// <summary>다음 스테이지 정보 창 (ResultButton.StageId)</summary>
        Next,
        /// <summary>이 스테이지 정보 창 (덱·부스터를 다시 고르고 시작)</summary>
        StageInfo,
    }
}
