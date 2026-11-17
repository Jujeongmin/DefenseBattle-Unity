using System.Collections.Generic;
using NUnit.Framework;

namespace DefenseBattle.Core.Tests
{
    /// <summary>봇 대전 규칙 (menu.ts startMatchmaking, pvp.ts findMatch·createBotSession, main.ts launch pvp)</summary>
    public class PvpRulesTests
    {
        sealed class FixedRandom : IRandom
        {
            readonly double m_v;
            public FixedRandom(double v) => m_v = v;
            public double Next() => m_v;
        }

        static EconState Fresh() => EconState.Fresh("S2026-11");

        [Test]
        public void FirstMatch_OnlyWithNoGames()
        {
            EconState e = Fresh();
            Assert.IsTrue(PvpRules.IsFirstMatch(e));
            e.Pvp.Losses = 1;
            Assert.IsFalse(PvpRules.IsFirstMatch(e));
        }

        [Test]
        public void SearchSeconds_Between1_5And3_5()
        {
            Assert.AreEqual(1.5, PvpRules.SearchSeconds(new FixedRandom(0)), 1e-9);
            double hi = PvpRules.SearchSeconds(new FixedRandom(0.999));
            Assert.Less(hi, 3.5);
            Assert.Greater(hi, 3.4);
        }

        [Test]
        public void PlayerOptions_UsesEconDecks()
        {
            EconState e = Fresh();
            e.AttackDeck = new List<AttackKey> { AttackKey.Ghost, AttackKey.Shield };
            GameOptions o = PvpRules.PlayerOptions(e, 1234u);
            Assert.AreEqual(GameMode.Pvp, o.Mode);
            Assert.AreEqual(1234u, o.Seed);
            CollectionAssert.AreEqual(e.AttackDeck, o.AttackDeck);
            CollectionAssert.AreEqual(e.Deck, o.Deck);
            Assert.IsTrue(o.UseResearch);
            Assert.AreNotSame(e.Deck, o.Deck, "판이 덱을 바꿔도 경제 상태는 그대로");
        }

        [Test]
        public void MakeBot_FirstThreeGamesWeakest()
        {
            EconState e = Fresh();
            e.Pvp.Wins = 2;
            BotMatch b = PvpRules.MakeBot(e, 77u, new FixedRandom(0.5));
            Assert.AreEqual(0.05, b.Skill, 1e-9);
            Assert.AreEqual(GameMode.Pvp, b.Game.Opts.Mode);
            Assert.AreEqual(77u, b.Game.Opts.Seed);
            e.Pvp.Losses = 1;
            Assert.Greater(PvpRules.MakeBot(e, 77u, new FixedRandom(0.5)).Skill, 0.05);
        }

        [Test]
        public void Clock_Formats()
        {
            Assert.AreEqual("0:00", PvpRules.Clock(0));
            Assert.AreEqual("1:05", PvpRules.Clock(65.4));
            Assert.AreEqual("10:00", PvpRules.Clock(600));
        }
    }
}
