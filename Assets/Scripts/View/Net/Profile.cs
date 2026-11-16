using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 닉네임 (서버 SetNick 이 정한 이름을 기기에 기억). 서버는 닉네임을 돌려주는 함수가 없어 마지막으로 정한 값을 보관한다.
    /// 처음엔 원본 meta.ts 처럼 "용사1234".
    /// </summary>
    public static class Profile
    {
        const string Key = "profile.nick";

        public static string Nick
        {
            get
            {
                string n = PlayerPrefs.GetString(Key, "");
                if (n.Length > 0) return n;
                n = Loc.Tl("용사{0}", Random.Range(1000, 10000));
                Nick = n;
                return n;
            }
            set
            {
                PlayerPrefs.SetString(Key, value ?? "");
                PlayerPrefs.Save();
            }
        }
    }
}
