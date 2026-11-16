using System.Collections.Generic;
using System.Threading.Tasks;
using DefenseBattle.Core;
using DefenseBattle.Services;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode;

namespace DefenseBattle.View
{
    /// <summary>
    /// 서버 함수 부르기 (원본 econ.ts call / act). 이름·인자는 Core.CloudApi.
    /// 경제 함수의 답(EconReply)은 EconCache 에 반영해 화면이 다시 그려지게 한다.
    /// </summary>
    public static class CloudClient
    {
        public static async Task<T> Call<T>(string function, Dictionary<string, object> args = null)
        {
            await UgsSession.SignInAsync();
            // SDK 의 기본 역직렬화 대신 JToken 으로 받아 서버와 같은 설정으로 읽는다
            JToken token = await CloudCodeService.Instance.CallModuleEndpointAsync<JToken>(CloudApi.Module, function, args);
            return CloudJson.Read<T>(token);
        }

        /// <summary>경제 함수: 답의 EconState 를 반영하고 동작 결과를 돌려준다</summary>
        public static async Task<T> Econ<T>(string function, Dictionary<string, object> args = null)
        {
            EconReply<T> r = await Call<EconReply<T>>(function, args);
            if (r?.Econ != null) EconCache.Apply(r.Econ);
            return r != null ? r.Result : default;
        }

        /// <summary>최신 경제 상태 받기</summary>
        public static async Task Sync()
        {
            EconReply r = await Call<EconReply>(CloudApi.EconSync);
            if (r?.Econ != null) EconCache.Apply(r.Econ);
        }

        public static Dictionary<string, object> Args(string k1, object v1) => new Dictionary<string, object> { { k1, v1 } };

        public static Dictionary<string, object> Args(string k1, object v1, string k2, object v2)
            => new Dictionary<string, object> { { k1, v1 }, { k2, v2 } };
    }
}
