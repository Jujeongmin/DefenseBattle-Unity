using System;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>프레임 선택 (render.ts WALK_SEQ / ATTACK_SEQ) + 유닛·몹 스프라이트 풀</summary>
    public class ActorViewsTests
    {
        GameObject m_root;
        ActorViews m_views;
        ShapeBatch m_under, m_mobOver, m_over;
        Game m_game;

        [SetUp]
        public void SetUp()
        {
            m_root = new GameObject("ActorViewsTest");
            m_views = m_root.AddComponent<ActorViews>();
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(SpriteCatalog.AssetPath);
            var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            m_views.Init(catalog, mat, mat);
            m_under = Batch("Under");
            m_mobOver = Batch("MobOver");
            m_over = Batch("Over");
            m_game = new Game(new GameOptions { Seed = 1, RandomSeed = 1 });
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(m_root);

        ShapeBatch Batch(string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(m_root.transform);
            return go.AddComponent<ShapeBatch>();
        }

        void Draw(double t)
        {
            m_under.Begin();
            m_mobOver.Begin();
            m_over.Begin();
            m_views.Draw(m_game, t, m_under, m_mobOver, m_over);
            m_under.End();
            m_mobOver.End();
            m_over.End();
        }

        static Mob NewMob(int id, Race race, double x, double y) =>
            new Mob { Id = id, Race = race, X = x, Y = y, Size = 40, Hp = 10, MaxHp = 10, SpeedMult = 1 };

        [Test]
        public void WalkCol_FollowsSequence()
        {
            Assert.AreEqual(1, SpriteAnim.WalkCol(0.00, 0, 0.16));
            Assert.AreEqual(0, SpriteAnim.WalkCol(0.17, 0, 0.16));
            Assert.AreEqual(1, SpriteAnim.WalkCol(0.33, 0, 0.16));
            Assert.AreEqual(2, SpriteAnim.WalkCol(0.49, 0, 0.16));
            Assert.AreEqual(1, SpriteAnim.WalkCol(0.65, 0, 0.16));
        }

        [Test]
        public void AttackCol_SwingsThenIdle()
        {
            Assert.AreEqual(1, SpriteAnim.AttackCol(0.1));
            Assert.AreEqual(2, SpriteAnim.AttackCol(0.074));
            Assert.AreEqual(0, SpriteAnim.AttackCol(0.049));
            Assert.AreEqual(1, SpriteAnim.AttackCol(0));
            Assert.AreEqual(7, SpriteAnim.FrameIndex(2, 1));
        }

        [Test]
        public void MobViews_FollowIdsNotObjects()
        {
            Mob m = NewMob(5, Race.Troll, 100, 100);
            m.HitFlash = 0.1;
            m_game.Mobs.Add(m);
            Draw(1);
            SpriteRenderer r5 = m_views.MobRenderer(5);
            Assert.IsNotNull(r5);
            StringAssert.StartsWith("Mob_Troll_", r5.sprite.name);
            Assert.Less(r5.color.a, 1f, "피격 중엔 반투명");

            // 엔진이 같은 객체를 다른 몹으로 다시 씀
            m.Id = 9;
            m.Race = Race.Undead;
            m.HitFlash = 0;
            Draw(2);
            Assert.IsNull(m_views.MobRenderer(5));
            SpriteRenderer r9 = m_views.MobRenderer(9);
            Assert.IsNotNull(r9);
            StringAssert.StartsWith("Mob_Undead_", r9.sprite.name);
            Assert.AreEqual(1f, r9.color.a, 1e-3f, "이전 몹의 피격 상태가 남지 않음");

            m_game.Mobs.Clear();
            Draw(3);
            Assert.AreEqual(0, m_views.ActiveCount);
        }

        [Test]
        public void UnitView_UsesGradeSheetAndFace()
        {
            var u = new Unit { Id = 3, Job = Job.Archer, Grade = GradeKey.Rare, GradeIndex = 1, X = 300, Y = 300, Face = 2 };
            m_game.Units.Add(u);
            Draw(1);
            SpriteRenderer r = m_views.UnitRenderer(3);
            Assert.IsNotNull(r);
            Assert.AreEqual("Archer_1_2_1", r.sprite.name, "등급 1 시트, 방향 2(우), 서 있기 열 1");
        }

        [Test]
        public void ActorViews_NoAllocSteadyState()
        {
            for (int i = 0; i < 60; i++) m_game.Mobs.Add(NewMob(100 + i, (Race)(i % 3), 70 + i * 5, 70));
            for (int i = 0; i < 20; i++)
                m_game.Units.Add(new Unit { Id = 500 + i, Job = (Job)(i % 8), Grade = (GradeKey)(i % 6), GradeIndex = i % 6, X = 200 + i * 10, Y = 300 });
            Draw(1);
            Draw(1.1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Draw(1.2);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
