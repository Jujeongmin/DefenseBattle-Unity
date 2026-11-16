using System;

namespace DefenseBattle.View
{
    /// <summary>
    /// 문자열을 만들지 않고 char[] 에 글자 쓰기 (매 프레임 숫자 표시용 - 문자열 조합 금지 규칙).
    /// 모든 함수는 다음 쓸 위치를 돌려준다. 버퍼가 모자라면 넘치는 글자는 버린다.
    /// </summary>
    public static class CharText
    {
        public static int Put(char[] b, int i, string s)
        {
            for (int k = 0; k < s.Length && i < b.Length; k++) b[i++] = s[k];
            return i;
        }

        public static int Put(char[] b, int i, char c)
        {
            if (i < b.Length) b[i++] = c;
            return i;
        }

        public static int PutInt(char[] b, int i, long v)
        {
            if (v < 0) { i = Put(b, i, '-'); v = -v; }
            int start = i;
            do
            {
                if (i >= b.Length) break;
                b[i++] = (char)('0' + v % 10);
                v /= 10;
            } while (v > 0);
            Array.Reverse(b, start, i - start);
            return i;
        }

        /// <summary>소수 첫째 자리까지 (JS toFixed(1) 과 같게 반올림)</summary>
        public static int PutFixed1(char[] b, int i, double v)
        {
            long tenths = (long)Math.Floor(v * 10 + 0.5);
            i = PutInt(b, i, tenths / 10);
            i = Put(b, i, '.');
            return Put(b, i, (char)('0' + tenths % 10));
        }
    }
}
