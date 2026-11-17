using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>상대 전장 스냅샷 보간 (oppRender.ts push · tick · draw 의 몹 위치 부분)</summary>
    public class SnapshotTimelineTests
    {
        const float Map = (float)Board.MapSize;

        static BoardSnapshot Snap(double t, params (int id, double x)[] mobs)
        {
            var s = new BoardSnapshot { T = t, Wave = 3, MobCount = mobs.Length };
            foreach (var (id, x) in mobs)
            {
                s.Mobs.Add(id);
                s.Mobs.Add(BoardSnapshot.Quantize(x));
                s.Mobs.Add(BoardSnapshot.Quantize(100));
                s.Mobs.Add(0);
                s.Mobs.Add(48);
                s.Mh.Add(100);
                s.Ms.Add(0);
            }
            return s;
        }

        static int IndexOf(OppFrame f, int id)
        {
            for (int i = 0; i < f.MobCount; i++) if (f.MobId[i] == id) return i;
            return -1;
        }

        [Test]
        public void Sample_InterpolatesMobBetweenSnapshots()
        {
            var tl = new SnapshotTimeline();
            tl.Push(Snap(1, (1, 0)), 0);
            tl.Push(Snap(2, (1, Map)), 0.5);
            var f = new OppFrame();
            Assert.IsTrue(tl.Sample(0.5, f));
            // 그릴 시점 = 최신 2 - Delay → 두 스냅 사이 (1 - Delay) 지점
            float expected = (float)(1 - SnapshotTimeline.Delay) * Map;
            Assert.AreEqual(expected, f.MobX[IndexOf(f, 1)], 3f);
            Assert.AreEqual(3, f.Wave);
        }

        [Test]
        public void Sample_NewMobAppearsOnlyAtLatest()
        {
            var tl = new SnapshotTimeline();
            tl.Push(Snap(1, (1, 0)), 0);
            tl.Push(Snap(2, (1, Map), (2, 50)), 0.5);
            var f = new OppFrame();
            tl.Sample(0.5, f);
            Assert.AreEqual(-1, IndexOf(f, 2), "아직 최신 스냅 시점 전");
            Assert.IsTrue(tl.Sample(10, f));
            Assert.GreaterOrEqual(IndexOf(f, 2), 0);
        }

        [Test]
        public void Sample_AfterGap_ExtrapolatesAtMostLimit()
        {
            var tl = new SnapshotTimeline();
            tl.Push(Snap(1, (1, 0)), 0);
            tl.Push(Snap(2, (1, Map)), 0.5);
            var f = new OppFrame();
            tl.Sample(10, f); // 다음 스냅이 한참 안 옴
            float limit = (float)(1 + SnapshotTimeline.Extrapolate / 1.0) * Map;
            Assert.AreEqual(limit, f.MobX[IndexOf(f, 1)], 3f, "마지막 움직임을 Extrapolate 만큼만 이어 그린다");
        }

        [Test]
        public void Push_IgnoresOlderSnapshot()
        {
            var tl = new SnapshotTimeline();
            tl.Push(Snap(2, (1, 300)), 0);
            tl.Push(Snap(1, (1, 0)), 0.1);
            Assert.AreEqual(1, tl.Count);
            var f = new OppFrame();
            tl.Sample(0.1, f);
            Assert.AreEqual(BoardSnapshot.Dequantize(BoardSnapshot.Quantize(300)), f.MobX[IndexOf(f, 1)], 0.5);
        }

        [Test]
        public void Push_CopiesData()
        {
            var tl = new SnapshotTimeline();
            BoardSnapshot s = Snap(1, (1, 300));
            tl.Push(s, 0);
            s.Mobs[1] = 0; // 보내는 쪽이 스냅샷 객체를 다시 씀
            var f = new OppFrame();
            tl.Sample(0, f);
            Assert.Greater(f.MobX[IndexOf(f, 1)], 200);
        }

        [Test]
        public void Sample_EmptyReturnsFalse()
        {
            Assert.IsFalse(new SnapshotTimeline().Sample(0, new OppFrame()));
        }
    }
}
