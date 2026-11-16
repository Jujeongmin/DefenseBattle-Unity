using System;

namespace DefenseBattle.Core
{
    /// <summary>상점 표시 규칙: 뽑기 비용, 확률 정보 표 (menu.ts openOdds - econ 규칙과 같은 수)</summary>
    public static class ShopRules
    {
        /// <summary>
        /// 뽑기 결제 방법: 뽑기권이 n장 이상이면 뽑기권, 아니면 보석 (서버 Gacha(count, ticket) 는 둘을 섞지 않는다).
        /// </summary>
        public static (bool Ticket, int Gems) GachaPlan(int tickets, int n)
        {
            n = n == 10 ? 10 : 1;
            if (tickets >= n) return (true, 0);
            return (false, n == 1 ? Chests.GachaOne : Chests.GachaTen);
        }

        public static int PoolSize(Rarity r)
        {
            int n = 0;
            foreach (UnitDef u in Units.All) if (u.Rarity == r) n++;
            return n;
        }

        /// <summary>확률 글자: 1% 미만은 소수 셋째 자리, 아니면 둘째 자리 (끝 0 없앰)</summary>
        public static string Pct(double p)
        {
            double v = p * 100;
            double r = Math.Round(v, v < 1 ? 3 : 2, MidpointRounding.AwayFromZero);
            return r.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>상자 한 줄 (지금 아레나 a 기준)</summary>
        public struct ChestOdds
        {
            public int Total;
            public int Rare;
            public int EpicBase;
            public double EpicChance;
            public double Legendary;
        }

        public static ChestOdds ChestRow(ChestDef d, int arena)
        {
            int total = d.Cards + d.PerArena * arena;
            int epicBase = (int)Math.Floor(d.Epic);
            return new ChestOdds
            {
                Total = total,
                EpicBase = epicBase,
                EpicChance = d.Epic % 1,
                Rare = Math.Min(total - epicBase, JsMath.RoundInt(d.Rare * (1 + arena * 0.25))),
                Legendary = d.Legendary,
            };
        }
    }
}
