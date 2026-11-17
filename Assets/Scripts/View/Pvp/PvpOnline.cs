using System;
using System.Threading.Tasks;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 실시간 대전에 쓰는 서버·Relay 연결 (없으면 대전은 늘 봇). 접속 뒤 UgsPvpServer · RelayLinkFactory 로 채운다.
    /// </summary>
    public static class PvpOnline
    {
        /// <summary>실제 상대를 찾는 최대 초 - 넘으면 봇 (pvp.ts BOT_FALLBACK_SEC)</summary>
        public const double BotFallbackSec = 8;

        public static IPvpServer Server;
        public static IPvpLinkFactory Links;

        public static bool Available => Server != null && Links != null;

        public static MatchSearch NewSearch()
            => new MatchSearch(Server, Links, () => Time.realtimeSinceStartupAsDouble, s => Task.Delay(TimeSpan.FromSeconds(s)));
    }
}
