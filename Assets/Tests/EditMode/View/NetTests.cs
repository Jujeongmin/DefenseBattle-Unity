using DefenseBattle.Core;
using NUnit.Framework;

namespace DefenseBattle.View.Tests
{
    /// <summary>서버 응답 읽기 + 기기 저장(LocalMeta)</summary>
    public class NetTests
    {
        [Test]
        public void EconReply_ReplacesDefaultCollections()
        {
            // 서버 Json.Settings 로 쓴 모양 (기본값이 든 목록을 덧붙이지 않고 바꿔야 한다)
            const string json = "{\"Econ\":{\"Crystals\":120,\"Items\":{\"GachaTicket\":2,\"GoldBooster\":1}," +
                                "\"UnitLevels\":{\"Archer\":3,\"Wizard\":1},\"Deck\":[0,1],\"AttackDeck\":[0,1,2,3]," +
                                "\"Stages\":{\"1-1\":3},\"Pvp\":{\"Trophies\":40}},\"Result\":7}";
            EconReply<int> r = CloudJson.Read<EconReply<int>>(json);
            Assert.AreEqual(7, r.Result);
            Assert.AreEqual(120, r.Econ.Crystals);
            Assert.AreEqual(2, r.Econ.Item(ItemKey.GachaTicket));
            Assert.AreEqual(3, r.Econ.LevelOf(Job.Archer));
            CollectionAssert.AreEqual(new[] { Job.Archer, Job.Wizard }, r.Econ.Deck);
            Assert.AreEqual(4, r.Econ.AttackDeck.Count, "기본 덱 4장 + 서버 4장 = 8장이 되면 안 됨");
            Assert.AreEqual(3, r.Econ.StarsOf("1-1"));
            Assert.AreEqual(40, r.Econ.Pvp.Trophies);
        }

        [Test]
        public void LocalMeta_ResetsDailyOnNewDay()
        {
            var m = new LocalMetaData();
            m.Normalize("2026-11-16");
            m.Daily.Progress["summon30"] = 12;
            m.BestWave = 30;
            m.Normalize("2026-11-16");
            Assert.AreEqual(12, m.Daily.Progress["summon30"], "같은 날은 그대로");
            m.Normalize("2026-11-17");
            Assert.AreEqual("2026-11-17", m.Daily.Date);
            Assert.AreEqual(0, m.Daily.Progress.Count, "날짜가 바뀌면 일일 진행 초기화");
            Assert.AreEqual(30, m.BestWave, "최고 기록은 유지");
        }

        [Test]
        public void LocalMeta_RoundTripsJson()
        {
            var m = new LocalMetaData { Tutorial = 2, StageFailStreak = 1 };
            m.SeenFeatures.Add("units");
            m.Normalize("2026-11-16");
            LocalMetaData back = LocalMetaData.FromJson(m.ToJson(), "2026-11-16");
            Assert.AreEqual(2, back.Tutorial);
            CollectionAssert.Contains(back.SeenFeatures, "units");
            Assert.IsNotNull(LocalMetaData.FromJson("not json", "2026-11-16"), "깨진 저장본은 새로");
        }
    }
}
