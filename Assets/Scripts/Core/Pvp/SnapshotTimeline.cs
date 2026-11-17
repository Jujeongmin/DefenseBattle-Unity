using System;

namespace DefenseBattle.Core
{
    /// <summary>
    /// 상대 전장 스냅샷 시간축 (oppRender.ts push · tick · draw 의 보간 부분).
    /// 받은 스냅샷을 복사해 최대 8개 들고, 그릴 시점(상대 게임 시간)을 매 프레임 실제 시간만큼 부드럽게 흘리면서
    /// 그 시점을 감싸는 두 스냅샷 사이로 몹 위치를 보간한다. 다음 스냅이 늦으면 마지막 움직임을 Extrapolate 만큼만 이어 그린다.
    /// 모든 버퍼는 미리 만들어 재사용한다 (매 프레임 할당 없음). 시간 단위는 초.
    /// </summary>
    public sealed class SnapshotTimeline
    {
        /// <summary>최신 스냅샷보다 이만큼(게임 초) 뒤를 그린다 - 다음 스냅이 오기 전에 따라잡지 않게 (oppRender.ts DELAY)</summary>
        public const double Delay = 0.35;
        /// <summary>스냅이 늦을 때 이어 그리는 최대 게임 초 (oppRender.ts EXTRAPOLATE)</summary>
        public const double Extrapolate = 0.25;
        const int MaxFrames = 8;

        sealed class Frame
        {
            public double T, At;
            public int MobN, UnitN;
            public readonly int[] Id = new int[OppFrame.MaxMobs], X = new int[OppFrame.MaxMobs], Y = new int[OppFrame.MaxMobs],
                Code = new int[OppFrame.MaxMobs], Size = new int[OppFrame.MaxMobs], Hp = new int[OppFrame.MaxMobs], St = new int[OppFrame.MaxMobs];
            public readonly int[] UJob = new int[OppFrame.MaxUnits], UGrade = new int[OppFrame.MaxUnits], UX = new int[OppFrame.MaxUnits],
                UY = new int[OppFrame.MaxUnits], UFace = new int[OppFrame.MaxUnits];
            public readonly int[] Gp = new int[(BoardSnapshot.GapSamples + BoardSnapshot.GapBits - 1) / BoardSnapshot.GapBits];
            public int GpN;
            public int Wave, Mobs, Units, BossPct, DeckN, AtkN;
            public readonly int[] Deck = new int[OppFrame.MaxDeck], Atk = new int[OppFrame.MaxDeck];
        }

        readonly Frame[] m_frames = new Frame[MaxFrames];
        int m_count;
        double m_clock = double.NaN;
        double m_lastDraw = double.NaN;

        public SnapshotTimeline()
        {
            for (int i = 0; i < MaxFrames; i++) m_frames[i] = new Frame();
        }

        public int Count => m_count;

        public void Clear()
        {
            m_count = 0;
            m_clock = double.NaN;
            m_lastDraw = double.NaN;
        }

        /// <summary>스냅샷 받음 (nowSec = 받은 쪽 실제 시각). 보낸 쪽 시간이 직전 이하이면(늦게 온 옛 것) 버린다</summary>
        public void Push(BoardSnapshot s, double nowSec)
        {
            if (s == null || s.V != BoardSnapshot.Version) return; // 구버전: 스프라이트 정보가 없다
            if (m_count > 0 && s.T <= m_frames[m_count - 1].T) return;
            Frame f;
            if (m_count < MaxFrames) f = m_frames[m_count++];
            else
            {
                // 가장 오래된 것을 다시 쓴다
                f = m_frames[0];
                Array.Copy(m_frames, 1, m_frames, 0, MaxFrames - 1);
                m_frames[MaxFrames - 1] = f;
            }
            Copy(s, f, nowSec);
        }

        static void Copy(BoardSnapshot s, Frame f, double at)
        {
            f.T = s.T;
            f.At = at;
            f.MobN = Math.Min(OppFrame.MaxMobs, Math.Min(s.Mobs.Count / 5, Math.Min(s.Mh.Count, s.Ms.Count)));
            for (int i = 0; i < f.MobN; i++)
            {
                int k = i * 5;
                f.Id[i] = s.Mobs[k];
                f.X[i] = s.Mobs[k + 1];
                f.Y[i] = s.Mobs[k + 2];
                f.Code[i] = s.Mobs[k + 3];
                f.Size[i] = s.Mobs[k + 4];
                f.Hp[i] = s.Mh[i];
                f.St[i] = s.Ms[i];
            }
            f.UnitN = Math.Min(OppFrame.MaxUnits, Math.Min(s.Units.Count / 4, s.Uc.Count));
            for (int i = 0; i < f.UnitN; i++)
            {
                int k = i * 4;
                f.UJob[i] = s.Units[k];
                f.UGrade[i] = s.Units[k + 1];
                f.UX[i] = s.Units[k + 2];
                f.UY[i] = s.Units[k + 3];
                f.UFace[i] = s.Uc[i];
            }
            f.GpN = Math.Min(f.Gp.Length, s.Gp.Count);
            for (int i = 0; i < f.GpN; i++) f.Gp[i] = s.Gp[i];
            f.Wave = s.Wave;
            f.Mobs = s.MobCount;
            f.Units = s.UnitCount;
            f.BossPct = s.BossHp >= 0 ? (int)Math.Round(s.BossHp * 100) : -1;
            f.DeckN = Math.Min(OppFrame.MaxDeck, s.Deck.Count);
            for (int i = 0; i < f.DeckN; i++) f.Deck[i] = s.Deck[i];
            f.AtkN = Math.Min(OppFrame.MaxDeck, s.Atk.Count);
            for (int i = 0; i < f.AtkN; i++) f.Atk[i] = s.Atk[i];
        }

        /// <summary>
        /// 그릴 시점(상대 게임 시간): 최신 스냅샷 시간 + 도착 뒤 흐른 시간 - Delay.
        /// 처음이거나 크게(실제 1초 이상) 어긋나면 바로 맞추고, 아니면 실제 시간만큼 흐르며 어긋남을 조금씩 따라잡는다.
        /// </summary>
        double Tick(double now)
        {
            Frame latest = m_frames[m_count - 1];
            double target = latest.T + (now - latest.At) * Waves.PlaySpeed - Delay;
            double dt = double.IsNaN(m_lastDraw) ? 0 : Math.Min(1, Math.Max(0, now - m_lastDraw));
            m_lastDraw = now;
            if (double.IsNaN(m_clock) || Math.Abs(target - m_clock) > Waves.PlaySpeed) m_clock = target;
            else
            {
                m_clock += dt * Waves.PlaySpeed;
                m_clock += (target - m_clock) * Math.Min(1, dt * 2);
            }
            return m_clock;
        }

        /// <summary>nowSec 의 장면을 into 에 채운다 (스냅샷이 아직 없으면 false)</summary>
        public bool Sample(double nowSec, OppFrame into)
        {
            if (m_count == 0) return false;
            double clock = Tick(nowSec);
            int i = 0;
            while (i < m_count - 1 && m_frames[i + 1].T <= clock) i++;
            bool late = i == m_count - 1 && i > 0;
            if (late) i--;
            Frame f0 = m_frames[i], f1 = m_frames[Math.Min(i + 1, m_count - 1)];
            double span = f1.T - f0.T;
            double maxA = late && span > 0 ? 1 + Extrapolate / span : 1;
            double a = span > 0 ? Math.Min(maxA, Math.Max(0, (clock - f0.T) / span)) : 1;

            // 몹: f0 에 있던 몹을 f1 위치 쪽으로. f1 에서 사라진 몹(처치)은 f1 시점 전까지만, 새 몹은 f1 에 닿아야 나타난다
            int n = 0;
            int hint = 0;
            for (int k = 0; k < f0.MobN && n < OppFrame.MaxMobs; k++)
            {
                int j = Find(f1, f0.Id[k], ref hint);
                if (j < 0)
                {
                    if (a < 1) Put(into, n++, f0, k, f0.X[k], f0.Y[k]);
                    continue;
                }
                Put(into, n++, f1, j, f0.X[k] + (f1.X[j] - f0.X[k]) * a, f0.Y[k] + (f1.Y[j] - f0.Y[k]) * a);
            }
            if (a >= 1)
            {
                hint = 0;
                for (int j = 0; j < f1.MobN && n < OppFrame.MaxMobs; j++)
                    if (Find(f0, f1.Id[j], ref hint) < 0) Put(into, n++, f1, j, f1.X[j], f1.Y[j]);
            }
            into.MobCount = n;

            // 유닛·빈틈·요약은 가까운 쪽(최신)으로
            Frame cur = f1;
            into.UnitCount = cur.UnitN;
            for (int k = 0; k < cur.UnitN; k++)
            {
                into.UnitJob[k] = cur.UJob[k];
                into.UnitGrade[k] = cur.UGrade[k];
                into.UnitFace[k] = cur.UFace[k];
                into.UnitX[k] = (float)BoardSnapshot.Dequantize(cur.UX[k]);
                into.UnitY[k] = (float)BoardSnapshot.Dequantize(cur.UY[k]);
            }
            for (int k = 0; k < into.Gap.Length; k++)
            {
                int w = k / BoardSnapshot.GapBits;
                into.Gap[k] = w < cur.GpN && (cur.Gp[w] & (1 << (k % BoardSnapshot.GapBits))) != 0;
            }
            into.Wave = cur.Wave;
            into.Mobs = cur.Mobs;
            into.Units = cur.Units;
            into.BossPct = cur.BossPct;
            into.DeckCount = cur.DeckN;
            Array.Copy(cur.Deck, into.Deck, cur.DeckN);
            into.AtkCount = cur.AtkN;
            Array.Copy(cur.Atk, into.Atk, cur.AtkN);

            // 지난 스냅샷 정리 (f0 앞 하나는 남김)
            if (i > 2)
            {
                int drop = i - 1;
                for (int k = 0; k < drop; k++)
                {
                    Frame old = m_frames[0];
                    Array.Copy(m_frames, 1, m_frames, 0, MaxFrames - 1);
                    m_frames[MaxFrames - 1] = old;
                }
                m_count -= drop;
            }
            return true;
        }

        /// <summary>id 의 위치 (없으면 -1). 몹 순서는 스냅샷마다 거의 같아 hint 에서부터 찾는다</summary>
        static int Find(Frame f, int id, ref int hint)
        {
            int n = f.MobN;
            for (int c = 0; c < n; c++)
            {
                int j = (hint + c) % n;
                if (f.Id[j] != id) continue;
                hint = j + 1;
                return j;
            }
            return -1;
        }

        static void Put(OppFrame o, int n, Frame f, int k, double qx, double qy)
        {
            o.MobId[n] = f.Id[k];
            o.MobCode[n] = f.Code[k];
            o.MobSize[n] = f.Size[k];
            o.MobHp[n] = f.Hp[k];
            o.MobStatus[n] = f.St[k];
            o.MobX[n] = (float)(qx / 255.0 * Board.MapSize);
            o.MobY[n] = (float)(qy / 255.0 * Board.MapSize);
        }
    }
}
