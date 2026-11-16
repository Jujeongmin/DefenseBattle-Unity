using System;
using DefenseBattle.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>이펙트 11종·숫자·보스바 (render.ts drawEffects / drawBossBar / short)</summary>
    public class EffectsViewTests
    {
        GameObject m_root;
        EffectsView m_view;
        FloatingTextPool m_texts;
        ShapeBatch m_over, m_top;
        Game m_game;
        readonly char[] m_buf = new char[32];

        [SetUp]
        public void SetUp()
        {
            m_root = new GameObject("EffectsTest");
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(SpriteCatalog.AssetPath);
            var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            m_texts = new GameObject("Texts").AddComponent<FloatingTextPool>();
            m_texts.transform.SetParent(m_root.transform);
            m_texts.Init(catalog.NumberFont);
            m_view = m_root.AddComponent<EffectsView>();
            m_view.Init(catalog, mat, m_texts, m_root.transform);
            m_over = Batch("Over");
            m_top = Batch("Top");
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
            m_over.Begin();
            m_top.Begin();
            m_texts.BeginFrame();
            m_view.Draw(m_game, t, m_over, m_top);
            m_texts.EndFrame();
            m_over.End();
            m_top.End();
        }

        string Short(double v) => new string(m_buf, 0, EffectsView.ShortNumber(v, m_buf, 0));

        [Test]
        public void ShortNumber_MatchesOriginal()
        {
            Assert.AreEqual("999", Short(999));
            Assert.AreEqual("9999", Short(9999));
            Assert.AreEqual("10.0K", Short(10000));
            Assert.AreEqual("12.3K", Short(12345));
            Assert.AreEqual("1.0M", Short(1_000_000));
            Assert.AreEqual("4.5M", Short(4_500_000));
            Assert.AreEqual("0", Short(0));
        }

        static Effect Fx(EffectType type, double x, double y, double ttl)
            => new Effect { Type = type, X = x, Y = y, X1 = x, Y1 = y, X2 = x + 80, Y2 = y + 40, Ttl = ttl, Dur = ttl * 2, R = 60, Color = 0xff8800, W = 6, Value = 1234, Seed = 0.3 };

        [Test]
        public void Effects_EveryTypeDrawsWithoutThrow()
        {
            foreach (EffectType type in (EffectType[])Enum.GetValues(typeof(EffectType)))
                m_game.Effects.Add(Fx(type, 300, 300, 0.2));
            m_game.Effects[0].Job = Job.Wizard;
            Assert.DoesNotThrow(() => Draw(1));
            Assert.Greater(m_over.VertexCount, 0);
            Assert.GreaterOrEqual(m_texts.ActiveCount, 2, "데미지·골드 숫자");
        }

        [Test]
        public void BossBar_ShowsWhenBossAlive()
        {
            Draw(1);
            Assert.AreEqual(0, m_top.VertexCount);
            m_game.Mobs.Add(new Mob { Id = 1, Boss = true, Hp = 50, MaxHp = 100, Size = 80, X = 70, Y = 70 });
            Draw(1);
            Assert.Greater(m_top.VertexCount, 0);
            Assert.AreEqual(1, m_texts.ActiveCount);
            StringAssert.StartsWith("BOSS  50%", m_texts.TextAt(0));
        }

        [Test]
        public void Effects_NoAllocSteadyState()
        {
            var types = (EffectType[])Enum.GetValues(typeof(EffectType));
            for (int i = 0; i < 240; i++)
            {
                Effect e = Fx(types[i % types.Length], 100 + i, 200, 0.1 + (i % 7) * 0.05);
                e.Job = (Job)(i % 32);
                e.Grade = i % 6;
                e.Crit = i % 2 == 0;
                m_game.Effects.Add(e);
            }
            m_game.Mobs.Add(new Mob { Id = 1, Boss = true, Hp = 50, MaxHp = 100, Size = 80, X = 70, Y = 70 });
            Draw(1);
            Draw(1.1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Draw(1.2);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
