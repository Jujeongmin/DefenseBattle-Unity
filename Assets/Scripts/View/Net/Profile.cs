using System;
using System.Threading.Tasks;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 닉네임 (서버 SetNick 이 정한 이름을 기기에 기억). 서버는 닉네임을 돌려주는 함수가 없어 마지막으로 정한 값을 보관한다.
    /// 처음엔 원본 meta.ts 처럼 "용사1234" 를 만들고 서버에도 한 번 알린다 (대전 상대·랭킹에 같은 이름이 보이게).
    /// </summary>
    public static class Profile
    {
        const string Key = "profile.nick";
        const string SyncedKey = "profile.nickSynced";

        public static string Nick
        {
            get
            {
                string n = PlayerPrefs.GetString(Key, "");
                if (n.Length > 0) return n;
                n = Loc.Tl("용사{0}", UnityEngine.Random.Range(1000, 10000));
                Nick = n;
                return n;
            }
            set
            {
                PlayerPrefs.SetString(Key, value ?? "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>서버에 닉네임 저장 (비속어·길이는 서버가 고친다) → 실제로 저장된 이름</summary>
        public static async Task<string> Save(string nick)
        {
            string clean = await CloudClient.Call<string>(CloudApi.SetNick, CloudClient.Args("nick", nick));
            if (!string.IsNullOrEmpty(clean))
            {
                Nick = clean;
                PlayerPrefs.SetInt(SyncedKey, 1);
                PlayerPrefs.Save();
            }
            return clean;
        }

        /// <summary>
        /// 접속 뒤 한 번: 이 기기가 아직 서버와 맞추지 않았으면, 서버에 저장된 이름이 있으면 그걸 쓰고(새 기기·계정 전환)
        /// 없을 때만 이 기기에서 만든 이름을 알린다
        /// </summary>
        public static async void EnsureSynced()
        {
            if (PlayerPrefs.GetInt(SyncedKey, 0) == 1) return;
            try
            {
                string saved = await CloudClient.Call<string>(CloudApi.GetNick);
                if (!string.IsNullOrEmpty(saved))
                {
                    Nick = saved;
                    PlayerPrefs.SetInt(SyncedKey, 1);
                    PlayerPrefs.Save();
                    return;
                }
                await Save(Nick);
            }
            catch (Exception ex) { Debug.LogWarning("[Profile] 닉네임 동기화 실패 (다음 접속 때 다시): " + ex.Message); }
        }

        /// <summary>다른 계정으로 바뀜 → 다음 동기화 때 그 계정의 이름을 받아 온다</summary>
        public static void ResetForAccountChange()
        {
            PlayerPrefs.DeleteKey(SyncedKey);
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
