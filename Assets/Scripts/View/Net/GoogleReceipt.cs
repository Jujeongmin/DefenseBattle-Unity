using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DefenseBattle.View
{
    /// <summary>Unity IAP 통합 영수증에서 Google 구매 원문·서명 꺼내기 ({"Store","TransactionID","Payload":"{\"json\",\"signature\"}"})</summary>
    public static class GoogleReceipt
    {
        public static bool TryParse(string unityReceipt, out string json, out string signature)
        {
            json = signature = null;
            if (string.IsNullOrEmpty(unityReceipt)) return false;
            try
            {
                JObject wrapper = JObject.Parse(unityReceipt);
                string payload = (string)wrapper["Payload"];
                if (string.IsNullOrEmpty(payload)) return false;
                JObject details = JObject.Parse(payload);
                json = (string)details["json"];
                signature = (string)details["signature"];
                return !string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(signature);
            }
            catch (JsonException) { return false; }
        }
    }
}
