using NUnit.Framework;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>논리 720 좌표 ↔ 월드 (32px = 1 유닛, 전장 중심 = 원점)</summary>
    public class BoardSpaceTests
    {
        [Test]
        public void ToWorld_MapsCornersAndCenter()
        {
            AssertNear(new Vector3(-11.25f, 11.25f, 0), BoardSpace.ToWorld(0, 0));
            AssertNear(Vector3.zero, BoardSpace.ToWorld(360, 360));
            AssertNear(new Vector3(11.25f, -11.25f, 0), BoardSpace.ToWorld(720, 720));
        }

        [Test]
        public void ToLogical_InvertsToWorld()
        {
            Vector2 p = BoardSpace.ToLogical(BoardSpace.ToWorld(123.5, 601));
            Assert.AreEqual(123.5f, p.x, 1e-3f);
            Assert.AreEqual(601f, p.y, 1e-3f);
        }

        static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
            Assert.AreEqual(expected.z, actual.z, 1e-4f);
        }
    }
}
