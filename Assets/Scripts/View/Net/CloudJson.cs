using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DefenseBattle.View
{
    /// <summary>
    /// 서버(CloudCode/Module/Logic/Json.cs)와 같은 JSON 설정. 특히 ObjectCreationHandling.Replace -
    /// EconState 처럼 기본값이 든 목록(AttackDeck 4장 등)에 서버 값을 덧붙이지 않고 바꾼다.
    /// </summary>
    public static class CloudJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public static T Read<T>(string json) => string.IsNullOrEmpty(json) ? default : JsonConvert.DeserializeObject<T>(json, Settings);

        public static T Read<T>(JToken token) => token == null || token.Type == JTokenType.Null ? default : token.ToObject<T>(Serializer);

        public static string Write(object value) => JsonConvert.SerializeObject(value, Settings);
    }
}
