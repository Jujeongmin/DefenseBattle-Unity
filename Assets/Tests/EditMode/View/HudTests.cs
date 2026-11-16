using DefenseBattle.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DefenseBattle.View.Tests
{
    /// <summary>HUD 규칙: 길게 눌러 소환 (ui.ts bind), 웨이브 문구, 값이 바뀔 때만 글자 갱신</summary>
    public class HudTests
    {
        static Game NewGame(int gold)
        {
            var g = new Game(new GameOptions { Seed = 1, RandomSeed = 1 });
            g.Gold = gold;
            return g;
        }

        [Test]
        public void SummonHold_RepeatsAfterDelayThenInterval()
        {
            Game g = NewGame(10000);
            var hold = new SummonHold();
            hold.PointerDown();
            Assert.AreEqual(0, hold.Tick(0.37f, g));
            Assert.AreEqual(1, hold.Tick(0.02f, g), "0.38초에 첫 소환");
            Assert.AreEqual(0, hold.Tick(0.13f, g));
            Assert.AreEqual(1, hold.Tick(0.02f, g), "그 뒤 0.14초마다");
            Assert.AreEqual(2, g.SummonCount);
        }

        [Test]
        public void SummonHold_StopsWhenUnaffordableOrReleased()
        {
            Game g = NewGame(10000);
            var hold = new SummonHold();
            hold.PointerDown();
            hold.Tick(0.4f, g);
            g.Gold = 0;
            hold.Tick(0.15f, g);
            Assert.IsTrue(hold.GoldShort, "골드 부족 알림");
            Assert.IsFalse(hold.Holding, "골드가 떨어지면 멈춤");
            g.Gold = 10000;
            Assert.AreEqual(0, hold.Tick(1f, g), "손을 떼기 전엔 다시 시작하지 않음");

            hold.PointerDown();
            hold.Tick(0.4f, g);
            hold.Release(); // 버튼 밖으로 나감 / 손 뗌
            Assert.AreEqual(0, hold.Tick(1f, g));

            hold.PointerDown();
            g.GameOver("test");
            Assert.AreEqual(0, hold.Tick(1f, g), "판이 끝나면 소환 없음");
        }

        [Test]
        public void SummonHold_PausedStopsRepeat()
        {
            Game g = NewGame(10000);
            var hold = new SummonHold();
            hold.PointerDown();
            hold.Tick(0.4f, g);
            g.Paused = true;
            Assert.AreEqual(0, hold.Tick(0.2f, g));
            Assert.IsFalse(hold.Holding);
        }

        [Test]
        public void Tap_SummonsOnce_HoldReleaseDoesNotDoubleCount()
        {
            Game g = NewGame(10000);
            var hold = new SummonHold();
            hold.PointerDown();
            hold.Release();
            Assert.IsTrue(hold.Click(g), "짧게 탭 = 1회");
            Assert.AreEqual(1, g.SummonCount);

            hold.PointerDown();
            hold.Tick(0.4f, g); // 길게 눌러 소환됨
            hold.Release();
            Assert.IsFalse(hold.Click(g), "길게 누른 뒤 손 뗄 때의 click 은 무시");
            Assert.AreEqual(2, g.SummonCount);
        }

        [Test]
        public void WaveLabel_Format()
        {
            var buf = new char[32];
            var stage = new Game(new BattleLaunch(BattleLaunch.Mode.Stage, "1-1").MakeOptions());
            stage.StartNextWave();
            stage.StartNextWave();
            stage.StartNextWave();
            Assert.AreEqual("Wave 3/" + Campaign.Get("1-1").Waves, new string(buf, 0, BattleHud.WaveLabel(stage, buf)));
            var endless = new Game(new BattleLaunch(BattleLaunch.Mode.Endless, null).MakeOptions());
            // 무한 모드도 총 50웨이브 (ui.ts: totalWaves 가 Infinity 인 건 PvP 뿐)
            Assert.AreEqual("Wave 1/" + Waves.Total, new string(buf, 0, BattleHud.WaveLabel(endless, buf)), "시작 전에도 1");
            var pvp = new Game(new GameOptions { Mode = GameMode.Pvp, Seed = 1 });
            pvp.StartNextWave();
            pvp.StartNextWave();
            Assert.AreEqual("Wave 2", new string(buf, 0, BattleHud.WaveLabel(pvp, buf)));
        }

        [Test]
        public void Hud_TextOnlyUpdatesOnChange()
        {
            var go = new GameObject("t", typeof(RectTransform));
            try
            {
                var text = new CachedText(go.AddComponent<TextMeshProUGUI>());
                var buf = new char[8];
                int n = CharText.PutInt(buf, 0, 123);
                text.Set(buf, n);
                text.Set(buf, n);
                Assert.AreEqual(1, text.SetCount);
                n = CharText.PutInt(buf, 0, 124);
                text.Set(buf, n);
                Assert.AreEqual(2, text.SetCount);
                text.Set("abc");
                text.Set("abc");
                Assert.AreEqual(3, text.SetCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
