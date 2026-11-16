using System;
using System.Globalization;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>기기 저장 (PlayerPrefs) - LocalMetaData 하나. 읽을 때마다 날짜를 확인해 일일 진행을 초기화한다</summary>
    public static class LocalMeta
    {
        const string Key = "meta.local.v1";
        static LocalMetaData s_data;

        /// <summary>UTC 날짜 "yyyy-MM-dd" (서버 EconContext.Today 와 같은 기준)</summary>
        public static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static LocalMetaData Data
        {
            get
            {
                if (s_data == null) s_data = LocalMetaData.FromJson(PlayerPrefs.GetString(Key, ""), Today);
                else s_data.Normalize(Today);
                return s_data;
            }
        }

        public static void Save()
        {
            if (s_data == null) return;
            PlayerPrefs.SetString(Key, s_data.ToJson());
            PlayerPrefs.Save();
        }

        /// <summary>일일 퀘스트 진행 더하기 (meta.ts addDailyProgress) - BestWave 는 최댓값</summary>
        public static void AddDaily(QuestStat stat, int amount)
        {
            LocalMetaData d = Data;
            foreach (DailyQuestDef q in Quests.Daily)
            {
                if (q.Stat != stat) continue;
                d.Daily.Progress.TryGetValue(q.Key, out int v);
                d.Daily.Progress[q.Key] = stat == QuestStat.BestWave ? Math.Max(v, amount) : v + amount;
            }
        }

        public static bool Seen(string feature) => Data.SeenFeatures.Contains(feature);

        public static void MarkSeen(string feature)
        {
            if (Seen(feature)) return;
            Data.SeenFeatures.Add(feature);
            Save();
        }

        /// <summary>테스트·설정 "처음부터": 기기 저장 지우기</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            s_data = null;
        }
    }
}
