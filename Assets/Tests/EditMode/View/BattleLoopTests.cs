using System.Collections.Generic;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>진행 루프 시간 나누기, 전장 화면 맞춤, 판 옵션</summary>
    public class BattleLoopTests
    {
        readonly List<double> m_steps = new List<double>();

        [Test]
        public void BattleClock_SplitsInto005Steps()
        {
            BattleClock.Steps(0.04f, Waves.PlaySpeed, m_steps);
            Assert.AreEqual(2, m_steps.Count);
            Assert.AreEqual(0.05, m_steps[0], 1e-6);
            Assert.AreEqual(0.03, m_steps[1], 1e-6);
        }

        [Test]
        public void BattleClock_ClampsLongFrames()
        {
            BattleClock.Steps(5f, Waves.PlaySpeed, m_steps);
            Assert.AreEqual(4, m_steps.Count);
            double sum = 0;
            foreach (double d in m_steps) sum += d;
            Assert.AreEqual(0.2, sum, 1e-6);
        }

        [Test]
        public void BattleClock_ZeroOrNegativeGivesNoSteps()
        {
            BattleClock.Steps(0f, Waves.PlaySpeed, m_steps);
            Assert.AreEqual(0, m_steps.Count);
            BattleClock.Steps(-1f, Waves.PlaySpeed, m_steps);
            Assert.AreEqual(0, m_steps.Count);
        }

        [TestCase(1080, 2400)] // 20:9 휴대폰
        [TestCase(1536, 2048)] // 4:3 태블릿
        [TestCase(720, 1280)]  // 16:9
        public void BoardFit_SquareBetweenBars(int w, int h)
        {
            var area = new Rect(0, 760, w, h - 220 - 760); // 아래 760px·위 220px 를 뺀 칸 (화면 px, 아래가 0)
            BoardSpace.FitOrtho(w, h, area, out float ortho, out Vector2 camPos);
            float pxPerUnit = h / (2f * ortho);
            float boardPx = BoardSpace.WorldSize * pxPerUnit;
            // 전장 중심(월드 원점)의 화면 위치
            float cx = w / 2f + (0 - camPos.x) * pxPerUnit;
            float cy = h / 2f + (0 - camPos.y) * pxPerUnit;
            Assert.AreEqual(Mathf.Min(area.width, area.height), boardPx, 0.5f, "칸에 꽉 맞는 정사각");
            Assert.GreaterOrEqual(cx - boardPx / 2, area.xMin - 0.5f);
            Assert.LessOrEqual(cx + boardPx / 2, area.xMax + 0.5f);
            Assert.GreaterOrEqual(cy - boardPx / 2, area.yMin - 0.5f);
            Assert.LessOrEqual(cy + boardPx / 2, area.yMax + 0.5f);
        }

        [Test]
        public void Launch_StageOptions()
        {
            GameOptions o = new BattleLaunch(BattleLaunch.Mode.Stage, "1-1").MakeOptions();
            Assert.AreEqual("1-1", o.StageId);
            Assert.AreEqual(Campaign.Get("1-1").Waves, o.WaveCount);
            CollectionAssert.AreEqual(Units.Starter, o.Deck);
            Assert.IsFalse(o.UseResearch);
        }

        [Test]
        public void Launch_EndlessOptions()
        {
            GameOptions o = new BattleLaunch(BattleLaunch.Mode.Endless, null).MakeOptions();
            Assert.IsNull(o.StageId);
            Assert.AreEqual(0, o.WaveCount);
        }
    }
}
