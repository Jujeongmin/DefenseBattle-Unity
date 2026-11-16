using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DefenseBattle.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DefenseBattle.Server
{
    /// <summary>
    /// Google Play 영수증 검증 (UGS Economy 대신 - 새 프로젝트는 Economy 를 쓸 수 없다).
    /// 1) Play Console 라이선스 공개 키로 서명 확인 (RSA SHA1, purchaseData 원문 그대로)
    /// 2) 패키지명·상품 id·구매 완료 상태 확인
    /// 3) obfuscatedAccountId 가 부른 플레이어의 PurchaseTag 와 같은지 (남의 영수증 재사용 차단)
    /// 같은 플레이어의 같은 주문 중복 지급은 GameServer 가 주문 id 기록으로 막는다. 환불·취소는 알 수 없다.
    /// </summary>
    public sealed class GooglePlayReceiptVerifier : IReceiptVerifier
    {
        readonly string m_packageName;
        readonly Func<Task<string>> m_licenseKey;

        /// <param name="licenseKey">Play Console "Base64 인코딩 RSA 공개 키" (없으면 모든 영수증 거절)</param>
        public GooglePlayReceiptVerifier(string packageName, Func<Task<string>> licenseKey)
        {
            m_packageName = packageName;
            m_licenseKey = licenseKey;
        }

        public async Task<ReceiptCheck> Verify(string playerId, string productId, string purchaseData, string signature, int localCost, string localCurrency)
        {
            var invalid = new ReceiptCheck { Valid = false };
            if (string.IsNullOrEmpty(purchaseData) || string.IsNullOrEmpty(signature)) return invalid;

            string key = await m_licenseKey();
            if (string.IsNullOrWhiteSpace(key) || !SignatureOk(key, purchaseData, signature)) return invalid;

            JObject data;
            try { data = JObject.Parse(purchaseData); }
            catch (JsonException) { return invalid; }

            if ((string)data["packageName"] != m_packageName) return invalid;
            if ((string)data["productId"] != productId) return invalid;
            // 0 = 구매 완료, 1 = 취소, 2 = 대기 (대기 중인 결제는 아직 지급하지 않는다)
            if ((int?)data["purchaseState"] is int state && state != 0) return invalid;
            if ((string)data["obfuscatedAccountId"] != PurchaseTag.ForPlayer(playerId)) return invalid;

            string orderId = (string)data["orderId"];
            if (string.IsNullOrEmpty(orderId)) orderId = (string)data["purchaseToken"];
            if (string.IsNullOrEmpty(orderId)) return invalid;
            return new ReceiptCheck { Valid = true, OrderId = orderId };
        }

        static bool SignatureOk(string base64Key, string purchaseData, string base64Signature)
        {
            try
            {
                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64Key.Trim()), out _);
                    return rsa.VerifyData(Encoding.UTF8.GetBytes(purchaseData), Convert.FromBase64String(base64Signature),
                        HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                }
            }
            catch (FormatException) { return false; }
            catch (CryptographicException) { return false; }
        }
    }
}
