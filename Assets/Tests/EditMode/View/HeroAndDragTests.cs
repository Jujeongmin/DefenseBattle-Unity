using System.Collections.Generic;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>구역 끌어 교환 (ui.ts setupDrag) + 영웅 관리 합성/판매 (ui.ts openHeroes)</summary>
    public class HeroAndDragTests
    {
        static Game NewGame()
        {
            var g = new Game(new GameOptions { Seed = 1, RandomSeed = 1 });
            g.Gold = 100000;
            return g;
        }

        static void Summon(Game g, Job job, GradeKey grade, int count)
        {
            var list = new List<KeyValuePair<Job, GradeKey>>();
            for (int i = 0; i < count; i++) list.Add(new KeyValuePair<Job, GradeKey>(job, grade));
            g.ScriptSummons(list);
            for (int i = 0; i < count; i++) g.Summon();
        }

        // ---------------- 구역 ----------------
        [Test]
        public void ZoneAt_Quadrants()
        {
            Assert.AreEqual(0, ZoneDragLogic.ZoneAt(new Vector2(360, 100)), "위");
            Assert.AreEqual(1, ZoneDragLogic.ZoneAt(new Vector2(620, 360)), "오른쪽");
            Assert.AreEqual(2, ZoneDragLogic.ZoneAt(new Vector2(360, 620)), "아래");
            Assert.AreEqual(3, ZoneDragLogic.ZoneAt(new Vector2(100, 360)), "왼쪽");
            Assert.AreEqual(-1, ZoneDragLogic.ZoneAt(new Vector2(100, 100)), "정대각선");
            Assert.AreEqual(-1, ZoneDragLogic.ZoneAt(new Vector2(800, 360)), "전장 밖");
        }

        [Test]
        public void ZoneDrag_SwapsOnRelease()
        {
            Game g = NewGame();
            g.Zones[0].Job = Job.Archer;
            g.Zones[2].Job = Job.Wizard;
            var d = new ZoneDragLogic();
            d.Down(0, new Vector2(360, 100), g, false);
            d.Move(0, new Vector2(360, 620));
            Assert.AreEqual(2, d.To, "대상 구역 표시");
            Assert.IsTrue(d.Up(0, new Vector2(360, 620), g, false));
            Assert.AreEqual(Job.Wizard, g.Zones[0].Job);
            Assert.AreEqual(Job.Archer, g.Zones[2].Job);
            Assert.IsFalse(d.Active);
        }

        [Test]
        public void ZoneDrag_CancelsOutsideBoard()
        {
            Game g = NewGame();
            g.Zones[0].Job = Job.Archer;
            var d = new ZoneDragLogic();

            d.Down(0, new Vector2(360, 100), g, false);
            Assert.IsFalse(d.Up(0, new Vector2(900, 360), g, false), "전장 밖에서 뗌");
            Assert.IsFalse(d.Active);

            d.Down(0, new Vector2(360, 100), g, false);
            d.Down(1, new Vector2(100, 360), g, false); // 두 번째 손가락
            Assert.IsFalse(d.Active);
            Assert.IsFalse(d.Up(0, new Vector2(360, 620), g, false));

            d.Down(0, new Vector2(360, 100), g, false);
            Assert.IsFalse(d.Up(0, new Vector2(360, 620), g, true), "패널이 열림");

            d.Down(0, new Vector2(360, 100), g, false);
            g.GameOver("test");
            Assert.IsFalse(d.Up(0, new Vector2(360, 620), g, false), "판 끝");
            Assert.AreEqual(Job.Archer, g.Zones[0].Job, "교환 없음");
        }

        // ---------------- 영웅 관리 ----------------
        [Test]
        public void HeroPanel_SellQty()
        {
            Game g = NewGame();
            Summon(g, Job.Archer, GradeKey.Common, 12);
            var p = new HeroPanelLogic();
            p.Open(g, Job.Archer, false);
            p.Qty = 1;
            Assert.IsNull(p.TapGrade(g, 0));
            Assert.AreEqual(11, g.CountOf(Job.Archer, GradeKey.Common));
            p.Qty = 10;
            p.TapGrade(g, 0);
            Assert.AreEqual(1, g.CountOf(Job.Archer, GradeKey.Common));
            p.Qty = HeroPanelLogic.All;
            Summon(g, Job.Archer, GradeKey.Common, 4);
            p.TapGrade(g, 0);
            Assert.AreEqual(0, g.CountOf(Job.Archer, GradeKey.Common));
        }

        [Test]
        public void HeroPanel_MergeNeedsThree()
        {
            Game g = NewGame();
            Summon(g, Job.Wizard, GradeKey.Common, 2);
            var p = new HeroPanelLogic();
            p.Open(g, null, true);
            Assert.AreEqual(g.Opts.Deck[0], p.Job, "합성할 게 없으면 덱 첫 직업 (ui.ts jobs()[0])");
            p.Job = Job.Wizard;
            Assert.AreEqual(Loc.Tl("같은 등급 {0}개가 필요해요", Grades.MergeCount), p.TapGrade(g, 0));
            Summon(g, Job.Wizard, GradeKey.Common, 1);
            Assert.IsNull(p.TapGrade(g, 0));
            Assert.AreEqual(1, g.CountOf(Job.Wizard, GradeKey.Rare));
        }

        [Test]
        public void HeroPanel_OpensOnMergeableFirst()
        {
            Game g = NewGame();
            Summon(g, Job.Archer, GradeKey.Common, 1);
            Summon(g, Job.Warrior, GradeKey.Common, 3);
            var p = new HeroPanelLogic();
            p.Open(g, null, null); // 영웅 버튼: 고른 직업·모드 없음
            Assert.AreEqual(Job.Warrior, p.Job);
            Assert.IsTrue(p.MergeMode, "합성할 게 있으면 합성 탭으로");
            CollectionAssert.IsSubsetOf(g.Opts.Deck, p.Jobs, "덱 직업 탭");
        }
    }
}
