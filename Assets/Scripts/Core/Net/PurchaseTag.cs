using System.Security.Cryptography;
using System.Text;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 결제를 플레이어에 묶는 표시. 기기는 결제할 때 Google Play 의 obfuscatedAccountId 에 이 값을 넣고,
    /// 서버는 영수증의 값이 부른 플레이어의 값과 같은지 본다 (남의 영수증을 가져와 받는 것을 막는다).
    /// 플레이어 id 를 그대로 보내지 않으려고 해시한다 (Google 권장, 64자 제한).
    /// </summary>
    public static class PurchaseTag
    {
        /// <summary>앱 패키지명 - 영수증의 packageName 과 같아야 한다</summary>
        public const string PackageName = "com.jeonja.defensebattle";

        /// <summary>SHA-256(플레이어 id) 소문자 16진수 64자</summary>
        public static string ForPlayer(string playerId)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(playerId ?? ""));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
