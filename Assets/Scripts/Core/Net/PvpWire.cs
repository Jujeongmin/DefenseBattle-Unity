using System;
using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 실시간 대전 전송 형식: 스냅샷·공격을 바이트로 (원본은 서버를 거치는 JSON pvpSnap · pvpAttack).
    /// 두 기기가 Relay 로 직접 주고받는다. 정수는 zigzag varint, 실수는 8바이트. 버퍼·목록을 재사용해 할당 없이 쓴다.
    /// 첫 바이트는 형식 번호 - 다른 버전 앱끼리 만나면 읽지 않는다.
    /// </summary>
    public static class PvpWire
    {
        /// <summary>스냅샷 버퍼 크기 (원본 스냅샷 최대 ~2.5KB 의 여유분)</summary>
        public const int MaxSnapshotBytes = 8192;
        const byte SnapFormat = 0xB1;
        const byte AttackFormat = 0xA1;

        public static int WriteSnapshot(BoardSnapshot s, byte[] buf)
        {
            int p = 0;
            buf[p++] = SnapFormat;
            p = Int(buf, p, s.V);
            p = Int(buf, p, s.Wave);
            p = Int(buf, p, s.MobCount);
            p = Int(buf, p, s.UnitCount);
            p = Int(buf, p, s.TopGrade);
            p = Int(buf, p, s.Gold);
            p = Dbl(buf, p, s.BossHp);
            p = Dbl(buf, p, s.T);
            p = List(buf, p, s.Mobs);
            p = List(buf, p, s.Units);
            p = List(buf, p, s.Uc);
            p = List(buf, p, s.Mh);
            p = List(buf, p, s.Ms);
            p = List(buf, p, s.Gp);
            p = List(buf, p, s.Deck);
            p = List(buf, p, s.Atk);
            p = Int(buf, p, s.Fx.Count);
            for (int i = 0; i < s.Fx.Count; i++)
            {
                int[] f = s.Fx[i];
                p = Int(buf, p, f.Length);
                for (int j = 0; j < f.Length; j++) p = Int(buf, p, f[j]);
            }
            return p;
        }

        /// <summary>into 의 목록을 비우고 채운다 (새 목록을 만들지 않음). 잘렸거나 다른 형식이면 false</summary>
        public static bool ReadSnapshot(byte[] buf, int len, BoardSnapshot into)
        {
            var r = new Reader(buf, len);
            if (len < 1 || buf[0] != SnapFormat) return false;
            r.P = 1;
            if (!r.Int(out into.V) || !r.Int(out into.Wave) || !r.Int(out into.MobCount) || !r.Int(out into.UnitCount)
                || !r.Int(out into.TopGrade) || !r.Int(out into.Gold) || !r.Dbl(out into.BossHp) || !r.Dbl(out into.T)) return false;
            if (!r.List(into.Mobs) || !r.List(into.Units) || !r.List(into.Uc) || !r.List(into.Mh) || !r.List(into.Ms)
                || !r.List(into.Gp) || !r.List(into.Deck) || !r.List(into.Atk)) return false;
            if (!r.Int(out int fxCount) || fxCount < 0 || fxCount > len) return false;
            List<int[]> fx = into.Fx;
            if (fx.Count > fxCount) fx.RemoveRange(fxCount, fx.Count - fxCount);
            for (int i = 0; i < fxCount; i++)
            {
                if (!r.Int(out int n) || n < 0 || n > len) return false;
                int[] f = i < fx.Count && fx[i].Length == n ? fx[i] : new int[n]; // 같은 길이면 재사용 (이펙트는 길이가 늘 같다)
                for (int j = 0; j < n; j++)
                    if (!r.Int(out f[j])) return false;
                if (i < fx.Count) fx[i] = f;
                else fx.Add(f);
            }
            return true;
        }

        public static int WriteAttack(AttackPayload a, byte[] buf)
        {
            int p = 0;
            buf[p++] = AttackFormat;
            p = Int(buf, p, a.Card);
            p = Int(buf, p, a.Wave);
            return Dbl(buf, p, a.Pos);
        }

        public static bool ReadAttack(byte[] buf, int len, out AttackPayload a)
        {
            a = default;
            if (len < 1 || buf[0] != AttackFormat) return false;
            var r = new Reader(buf, len) { P = 1 };
            return r.Int(out a.Card) && r.Int(out a.Wave) && r.Dbl(out a.Pos);
        }

        static int Int(byte[] b, int p, int v)
        {
            uint z = (uint)((v << 1) ^ (v >> 31));
            while (z >= 0x80)
            {
                b[p++] = (byte)(z | 0x80);
                z >>= 7;
            }
            b[p++] = (byte)z;
            return p;
        }

        static int Dbl(byte[] b, int p, double v)
        {
            ulong u = (ulong)BitConverter.DoubleToInt64Bits(v);
            for (int i = 0; i < 8; i++) b[p++] = (byte)(u >> (i * 8));
            return p;
        }

        static int List(byte[] b, int p, List<int> l)
        {
            p = Int(b, p, l.Count);
            for (int i = 0; i < l.Count; i++) p = Int(b, p, l[i]);
            return p;
        }

        struct Reader
        {
            readonly byte[] m_b;
            readonly int m_len;
            public int P;

            public Reader(byte[] b, int len)
            {
                m_b = b;
                m_len = len;
                P = 0;
            }

            public bool Int(out int v)
            {
                v = 0;
                uint z = 0;
                for (int shift = 0; shift < 35; shift += 7)
                {
                    if (P >= m_len) return false;
                    byte x = m_b[P++];
                    z |= (uint)(x & 0x7f) << shift;
                    if (x < 0x80)
                    {
                        v = (int)(z >> 1) ^ -(int)(z & 1);
                        return true;
                    }
                }
                return false;
            }

            public bool Dbl(out double v)
            {
                v = 0;
                if (P + 8 > m_len) return false;
                ulong u = 0;
                for (int i = 0; i < 8; i++) u |= (ulong)m_b[P++] << (i * 8);
                v = BitConverter.Int64BitsToDouble((long)u);
                return true;
            }

            public bool List(List<int> into)
            {
                into.Clear();
                if (!Int(out int n) || n < 0 || n > m_len) return false;
                for (int i = 0; i < n; i++)
                {
                    if (!Int(out int x)) return false;
                    into.Add(x);
                }
                return true;
            }
        }
    }
}
