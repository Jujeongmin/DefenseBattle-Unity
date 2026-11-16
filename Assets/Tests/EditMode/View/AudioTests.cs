using DefenseBattle.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>전투 소리 규칙 (audio.ts): 직업별 공격음, 같은 소리 최소 간격, 음소거 저장, 소리 목록</summary>
    public class AudioTests
    {
        [Test]
        public void AttackSfx_MatchesOriginalJobMapping()
        {
            // ui.ts: archer·ranger → 화살, wizard·frost·bomber → 마법, 나머지 → 검
            Assert.AreEqual(Sfx.Arrow, SoundRules.AttackSfx(Job.Archer));
            Assert.AreEqual(Sfx.Arrow, SoundRules.AttackSfx(Job.Ranger));
            Assert.AreEqual(Sfx.Magic, SoundRules.AttackSfx(Job.Wizard));
            Assert.AreEqual(Sfx.Magic, SoundRules.AttackSfx(Job.Frost));
            Assert.AreEqual(Sfx.Magic, SoundRules.AttackSfx(Job.Bomber));
            Assert.AreEqual(Sfx.Sword, SoundRules.AttackSfx(Job.Warrior));
            Assert.AreEqual(Sfx.Sword, SoundRules.AttackSfx(Job.Crossbow), "원본은 이름으로만 나눈다 (석궁도 검)");
        }

        [Test]
        public void Gate_BlocksSameSoundWithinMinGap()
        {
            var gate = new SfxGate();
            Assert.IsTrue(gate.Allow(Sfx.Arrow, 1000, 90));
            Assert.IsFalse(gate.Allow(Sfx.Arrow, 1080, 90), "90ms 안");
            Assert.IsTrue(gate.Allow(Sfx.Magic, 1080, 140), "다른 소리는 따로");
            Assert.IsTrue(gate.Allow(Sfx.Arrow, 1091, 90));
            Assert.IsTrue(gate.Allow(Sfx.Merge, 1092, 0), "간격 없음");
            Assert.IsTrue(gate.Allow(Sfx.Merge, 1092, 0));
        }

        [Test]
        public void MinGaps_MatchOriginal()
        {
            Assert.AreEqual(90, SoundRules.MinGapMs(Sfx.Arrow));
            Assert.AreEqual(140, SoundRules.MinGapMs(Sfx.Magic));
            Assert.AreEqual(110, SoundRules.MinGapMs(Sfx.Sword));
            Assert.AreEqual(60, SoundRules.MinGapMs(Sfx.Hit));
            Assert.AreEqual(40, SoundRules.MinGapMs(Sfx.Button));
            Assert.AreEqual(60, SoundRules.MinGapMs(Sfx.Coin));
            Assert.AreEqual(50, SoundRules.MinGapMs(Sfx.Summon));
            Assert.AreEqual(0, SoundRules.MinGapMs(Sfx.BossKill));
        }

        [Test]
        public void Mute_IsSaved()
        {
            bool was = SoundSettings.Muted;
            try
            {
                SoundSettings.Muted = true;
                Assert.IsTrue(SoundSettings.Muted);
                Assert.AreEqual(1, PlayerPrefs.GetInt(SoundSettings.MuteKey));
                SoundSettings.Muted = false;
                Assert.AreEqual(0, PlayerPrefs.GetInt(SoundSettings.MuteKey));
            }
            finally
            {
                SoundSettings.Muted = was;
            }
        }

        [Test]
        public void Catalog_HasEveryClip()
        {
            var c = AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath);
            Assert.IsNotNull(c, "SoundCatalog 없음 - Build Battle Assets");
            foreach (Sfx s in (Sfx[])System.Enum.GetValues(typeof(Sfx))) Assert.IsNotNull(c.Clip(s), s.ToString());
            foreach (Bgm b in (Bgm[])System.Enum.GetValues(typeof(Bgm))) Assert.IsNotNull(c.Track(b), b.ToString());
        }
    }
}
