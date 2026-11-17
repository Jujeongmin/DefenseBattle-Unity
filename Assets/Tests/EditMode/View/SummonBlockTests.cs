using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefenseBattle.View.Tests
{
    /// <summary>대전 카운트다운·첫 대전 던지기 안내 동안 소환 탭을 막는다 (ui.ts: if (this.summonLock || this.countingDown()) return)</summary>
    public class SummonBlockTests
    {
        GameObject m_go;
        SummonButton m_button;
        Game m_game;

        [SetUp]
        public void SetUp()
        {
            m_go = new GameObject("Summon", typeof(RectTransform), typeof(Image), typeof(Button));
            m_button = m_go.AddComponent<SummonButton>();
            m_game = new Game(new GameOptions { RandomSeed = 1 });
            m_button.Game = m_game;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_go);

        [Test]
        public void Tap_Blocked_DoesNotSummon()
        {
            m_button.Blocked = () => true;
            m_button.OnPointerClick(new PointerEventData(null));
            Assert.AreEqual(0, m_game.Units.Count);
        }

        [Test]
        public void Tap_NotBlocked_Summons()
        {
            m_button.Blocked = () => false;
            m_button.OnPointerClick(new PointerEventData(null));
            Assert.AreEqual(1, m_game.Units.Count);
        }
    }
}
