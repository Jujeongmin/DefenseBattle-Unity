using System.Collections.Generic;

namespace DefenseBattle.View
{
    /// <summary>
    /// 기기에만 두는 진행 (원본 meta.ts 중 서버 경제가 아닌 것): 일일 퀘스트 진행, 최고 웨이브, 본 기능(NEW),
    /// 튜토리얼 단계, 특가 표시 시각, 스테이지 연속 실패, 마지막으로 본 아레나.
    /// </summary>
    public sealed class LocalMetaData
    {
        public DailyProgress Daily = new DailyProgress();
        public int BestWave;
        public List<string> SeenFeatures = new List<string>();
        /// <summary>0 = 처음, 1 = 1-1 진행 중, 2 = 1-1 클리어 (main.ts meta.tutorial)</summary>
        public int Tutorial;
        /// <summary>특가 id → 마지막 표시 시각 (ms)</summary>
        public Dictionary<string, long> OfferShownAt = new Dictionary<string, long>();
        public int StageFailStreak;
        /// <summary>승급 팝업용 (-1 = 아직 모름)</summary>
        public int ArenaMax = -1;

        /// <summary>빈 칸 채우기 + 날짜가 바뀌었으면 일일 진행 초기화</summary>
        public void Normalize(string today)
        {
            if (SeenFeatures == null) SeenFeatures = new List<string>();
            if (OfferShownAt == null) OfferShownAt = new Dictionary<string, long>();
            if (Daily == null || Daily.Date != today) Daily = new DailyProgress { Date = today };
            if (Daily.Progress == null) Daily.Progress = new Dictionary<string, int>();
        }

        public string ToJson() => CloudJson.Write(this);

        /// <summary>저장본 읽기 (없거나 깨졌으면 새로)</summary>
        public static LocalMetaData FromJson(string json, string today)
        {
            LocalMetaData m;
            try { m = CloudJson.Read<LocalMetaData>(json); }
            catch (Newtonsoft.Json.JsonException) { m = null; }
            m = m ?? new LocalMetaData();
            m.Normalize(today);
            return m;
        }
    }
}
