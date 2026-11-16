using System.Collections.Generic;

namespace DefenseBattle.View
{
    /// <summary>결과·확인 창 내용 (ui.ts ResultView)</summary>
    public sealed class ResultView
    {
        public bool Win;
        public string Title;
        public string Sub;
        /// <summary>별 (없으면 null - 별 줄을 숨김)</summary>
        public int? Stars;
        public readonly List<string> Rewards = new List<string>();
        /// <summary>작은 안내 (원본 HTML 그대로일 수 있음 - 표시 때 태그를 지운다)</summary>
        public string Extra;
        public readonly List<ResultButton> Buttons = new List<ResultButton>();
    }
}
