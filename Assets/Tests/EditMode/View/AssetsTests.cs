using DefenseBattle.Core;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>전투 화면 에셋 (Tools → Defense Battle → Build Battle Assets 결과)</summary>
    public class AssetsTests
    {
        static SpriteCatalog Catalog()
        {
            var c = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(SpriteCatalog.AssetPath);
            Assert.IsNotNull(c, "SpriteCatalog 없음 - Build Battle Assets 메뉴를 실행할 것");
            return c;
        }

        [Test]
        public void Catalog_HasEverySheet()
        {
            SpriteCatalog c = Catalog();
            foreach (UnitDef u in Units.All)
                for (int g = 0; g < Grades.Count; g++)
                {
                    Sprite[] f = c.Unit(u.Job, g);
                    Assert.AreEqual(12, f.Length, u.Sheet + "_" + g);
                    foreach (Sprite s in f) Assert.IsNotNull(s, u.Sheet + "_" + g);
                }
            for (int r = 0; r < Races.All.Length; r++)
                for (int v = 0; v < Races.Variants[r]; v++)
                    Assert.AreEqual(Races.Sprite[r] + "_" + v + "_0_0", c.Mob(Races.All[r], v)[0].name);
            foreach (AttackDef a in Attacks.All)
                Assert.AreEqual(Races.Sprite[(int)a.Race] + "_" + a.Variant + "_0_0", c.Mob(a.Race, a.Variant)[0].name, a.Id);
        }

        [Test]
        public void Catalog_UnknownMobVariantFallsBackToZero()
        {
            SpriteCatalog c = Catalog();
            Assert.AreSame(c.Mob(Race.Troll, 0), c.Mob(Race.Troll, 99));
        }

        [Test]
        public void IconSpriteAsset_CoversLocIcons()
        {
            TMP_SpriteAsset icons = TMP_Settings.defaultSpriteAsset;
            Assert.IsNotNull(icons, "TMP 기본 Sprite Asset 없음");
            foreach (string name in new[] { "coin", "timer", "pause", "play", "sound", "mute", "helmet", "home", "hammer", "up", "down", "star", "tv", "warning", "close" })
                Assert.GreaterOrEqual(icons.GetSpriteIndexFromName(name), 0, name);
        }

        [Test]
        public void Field_Is720Square()
        {
            Sprite field = Catalog().Field;
            Assert.IsNotNull(field);
            Assert.AreEqual(720f, field.rect.width);
            Assert.AreEqual(720f, field.rect.height);
            Assert.AreEqual(32f, field.pixelsPerUnit);
        }

        [Test]
        public void Fonts_AreGalmuri()
        {
            SpriteCatalog c = Catalog();
            Assert.IsNotNull(c.Font);
            Assert.IsNotNull(c.BoldFont);
            Assert.AreEqual(TMPro.AtlasPopulationMode.Dynamic, c.Font.atlasPopulationMode);
        }
    }
}
