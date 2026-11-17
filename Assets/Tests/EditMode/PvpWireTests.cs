using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>실시간 대전 전송 형식 (원본은 JSON - 여기선 Relay 로 보내는 바이트)</summary>
    public class PvpWireTests
    {
        static BoardSnapshot Sample()
        {
            var s = new BoardSnapshot
            {
                Wave = 17, MobCount = 42, UnitCount = 9, TopGrade = -1, Gold = 123456, BossHp = 0.375, T = 81.25,
            };
            s.Mobs.AddRange(new[] { 1, 255, 0, 0x7f, 3, 900000, 12, 34, 5, 2 });
            s.Units.AddRange(new[] { 4, 2, 100, 200 });
            s.Uc.Add(5);
            s.Mh.AddRange(new[] { 100, 0 });
            s.Ms.AddRange(new[] { 0, 64 });
            s.Gp.AddRange(new[] { 0, 1048575, -7 });
            s.Fx.Add(new[] { 1, 2, 3 });
            s.Fx.Add(new int[0]);
            s.Deck.AddRange(new[] { 0, 3, 5, 31 });
            s.Atk.AddRange(new[] { 15, 0, 7, 2 });
            return s;
        }

        [Test]
        public void Snapshot_RoundTrips()
        {
            BoardSnapshot a = Sample();
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            int n = PvpWire.WriteSnapshot(a, buf);
            Assert.Greater(n, 0);
            var b = new BoardSnapshot();
            Assert.IsTrue(PvpWire.ReadSnapshot(buf, n, b));
            Assert.AreEqual(a.Wave, b.Wave);
            Assert.AreEqual(a.MobCount, b.MobCount);
            Assert.AreEqual(a.UnitCount, b.UnitCount);
            Assert.AreEqual(a.TopGrade, b.TopGrade);
            Assert.AreEqual(a.Gold, b.Gold);
            Assert.AreEqual(a.BossHp, b.BossHp);
            Assert.AreEqual(a.T, b.T);
            Assert.AreEqual(a.V, b.V);
            CollectionAssert.AreEqual(a.Mobs, b.Mobs);
            CollectionAssert.AreEqual(a.Units, b.Units);
            CollectionAssert.AreEqual(a.Uc, b.Uc);
            CollectionAssert.AreEqual(a.Mh, b.Mh);
            CollectionAssert.AreEqual(a.Ms, b.Ms);
            CollectionAssert.AreEqual(a.Gp, b.Gp);
            CollectionAssert.AreEqual(a.Deck, b.Deck);
            CollectionAssert.AreEqual(a.Atk, b.Atk);
            Assert.AreEqual(2, b.Fx.Count);
            CollectionAssert.AreEqual(a.Fx[0], b.Fx[0]);
            Assert.AreEqual(0, b.Fx[1].Length);
        }

        [Test]
        public void Snapshot_ReadReusesLists()
        {
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            var into = new BoardSnapshot();
            PvpWire.ReadSnapshot(buf, PvpWire.WriteSnapshot(Sample(), buf), into);
            List<int> mobs = into.Mobs;
            var small = new BoardSnapshot { Wave = 2 };
            small.Mobs.Add(9);
            Assert.IsTrue(PvpWire.ReadSnapshot(buf, PvpWire.WriteSnapshot(small, buf), into));
            Assert.AreSame(mobs, into.Mobs, "목록은 새로 만들지 않는다");
            CollectionAssert.AreEqual(new[] { 9 }, into.Mobs);
            Assert.AreEqual(0, into.Units.Count);
            Assert.AreEqual(0, into.Fx.Count);
            Assert.AreEqual(-1, into.BossHp);
        }

        [Test]
        public void Attack_RoundTrips()
        {
            var buf = new byte[64];
            var a = new AttackPayload { Card = 15, Wave = 33, Pos = 0.7031 };
            int n = PvpWire.WriteAttack(a, buf);
            Assert.IsTrue(PvpWire.ReadAttack(buf, n, out AttackPayload b));
            Assert.AreEqual(a.Card, b.Card);
            Assert.AreEqual(a.Wave, b.Wave);
            Assert.AreEqual(a.Pos, b.Pos);
        }

        [Test]
        public void Read_Truncated_ReturnsFalse()
        {
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            int n = PvpWire.WriteSnapshot(Sample(), buf);
            Assert.IsFalse(PvpWire.ReadSnapshot(buf, n / 2, new BoardSnapshot()));
            Assert.IsFalse(PvpWire.ReadSnapshot(buf, 0, new BoardSnapshot()));
            int m = PvpWire.WriteAttack(new AttackPayload { Card = 1 }, buf);
            Assert.IsFalse(PvpWire.ReadAttack(buf, m - 1, out _));
        }

        [Test]
        public void Read_WrongVersionByte_ReturnsFalse()
        {
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            int n = PvpWire.WriteSnapshot(Sample(), buf);
            buf[0] = 0xEE;
            Assert.IsFalse(PvpWire.ReadSnapshot(buf, n, new BoardSnapshot()));
        }

        [Test]
        public void WriteRead_SteadyState_NoAllocation()
        {
#if UNITY_EDITOR
            Assert.Ignore("CoreTests (.NET) 에서 측정");
#endif
            BoardSnapshot a = Sample();
            var buf = new byte[PvpWire.MaxSnapshotBytes];
            var into = new BoardSnapshot();
            for (int i = 0; i < 200; i++) // 데워 두기 (JIT 단계 전환도 끝나게)
            {
                PvpWire.ReadSnapshot(buf, PvpWire.WriteSnapshot(a, buf), into);
                PvpWire.ReadAttack(buf, PvpWire.WriteAttack(new AttackPayload { Card = i % 16 }, buf), out _);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                int n = PvpWire.WriteSnapshot(a, buf);
                PvpWire.ReadSnapshot(buf, n, into);
                PvpWire.ReadAttack(buf, PvpWire.WriteAttack(new AttackPayload { Card = i % 16 }, buf), out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before; // Assert 인자 박싱보다 먼저 잰다
            Assert.AreEqual(0, allocated);
        }
    }
}
