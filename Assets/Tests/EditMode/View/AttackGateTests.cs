using DefenseBattle.Core;
using NUnit.Framework;

namespace DefenseBattle.View.Tests
{
    /// <summary>상대가 보낸 몹 검사 (원본 서버 pvpAttack 의 웨이브 제한 - 이제 기기끼리 직접 받으니 받는 쪽이 막는다)</summary>
    public class AttackGateTests
    {
        [Test]
        public void HugeWave_ClampedNearMine()
        {
            var gate = new AttackGate();
            var p = new AttackPayload { Card = 1, Wave = int.MaxValue, Pos = 0.5 };
            Assert.IsTrue(gate.Allow(ref p, 7, 0));
            Assert.AreEqual(7 + AttackGate.WaveSlack, p.Wave);
        }

        [Test]
        public void ZeroOrNegativeWave_RaisedToOne()
        {
            var gate = new AttackGate();
            var p = new AttackPayload { Wave = -5 };
            Assert.IsTrue(gate.Allow(ref p, 3, 0));
            Assert.AreEqual(1, p.Wave);
        }

        [Test]
        public void NormalWave_Unchanged()
        {
            var gate = new AttackGate();
            var p = new AttackPayload { Wave = 9 };
            gate.Allow(ref p, 8, 0);
            Assert.AreEqual(9, p.Wave);
        }

        [Test]
        public void Flood_LimitedPerSecond()
        {
            var gate = new AttackGate();
            int allowed = 0;
            for (int i = 0; i < 50; i++)
            {
                var p = new AttackPayload { Wave = 1 };
                if (gate.Allow(ref p, 1, 0.5 + i * 0.001)) allowed++;
            }
            Assert.AreEqual(AttackGate.MaxPerSecond, allowed);
            var later = new AttackPayload { Wave = 1 };
            Assert.IsTrue(gate.Allow(ref later, 1, 1.6), "1초 뒤엔 다시 받는다");
        }
    }
}
