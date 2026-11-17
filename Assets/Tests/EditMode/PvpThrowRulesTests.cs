using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>몹 카드를 지금 던질 수 있나 (ui.ts attack-card disabled 조건) - 버튼 상태가 아니라 게임 상태로 판단</summary>
    public class PvpThrowRulesTests
    {
        [Test]
        public void CanThrow_NeedsGoldAndNoCooldown()
        {
            var g = new Game(new GameOptions { Mode = GameMode.Pvp, RandomSeed = 1 });
            int card = g.AttackCards[0];
            g.Gold = g.AttackCost(card);
            Assert.IsTrue(PvpRules.CanThrow(g, card), "카운트다운이 끝난 첫 프레임에도 골드만 있으면 던질 수 있다");
            g.Gold = g.AttackCost(card) - 1;
            Assert.IsFalse(PvpRules.CanThrow(g, card));
        }

        [Test]
        public void CanThrow_FalseAfterOver()
        {
            var g = new Game(new GameOptions { Mode = GameMode.Pvp, RandomSeed = 1 });
            g.Gold = 9999;
            g.PvpEnd(true, "");
            Assert.IsFalse(PvpRules.CanThrow(g, g.AttackCards[0]));
        }
    }
}
