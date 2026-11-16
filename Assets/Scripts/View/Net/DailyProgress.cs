using System.Collections.Generic;

namespace DefenseBattle.View
{
    /// <summary>오늘의 일일 퀘스트 진행 (meta.ts daily - 받았는지는 서버 OneTime 이 안다)</summary>
    public sealed class DailyProgress
    {
        /// <summary>UTC "yyyy-MM-dd"</summary>
        public string Date;
        /// <summary>퀘스트 키 → 진행 값</summary>
        public Dictionary<string, int> Progress = new Dictionary<string, int>();
    }
}
