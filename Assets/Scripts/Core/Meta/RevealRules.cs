using System.Collections.Generic;

namespace DefenseBattle.Core
{
    /// <summary>카드 공개 연출 순서·시간 (chests.ts showReveal): 낮은 등급부터, 영웅 이상은 뜸을 들인다</summary>
    public static class RevealRules
    {
        /// <summary>상자 떨어짐 → 흔들림 3번 → 터짐 → 카드 (ms)</summary>
        public static readonly int[] Shakes = { 520, 860, 1200 };
        public const int Burst = 1480;
        public const int Cards = 1760;

        /// <summary>서버는 좋은 카드부터 준다 → 거꾸로 (가장 좋은 카드가 마지막)</summary>
        public static List<CardDrop> Order(List<CardDrop> drops)
        {
            var list = new List<CardDrop>(drops);
            list.Reverse();
            return list;
        }

        /// <summary>카드마다 나오는 시각 (누적): 영웅 이상 +620ms, 나머지 +200ms</summary>
        public static int[] Delays(List<CardDrop> order)
        {
            var d = new int[order.Count];
            int t = 0;
            for (int i = 0; i < order.Count; i++)
            {
                t += Units.Get(order[i].Job).Rarity >= Rarity.Epic ? 620 : 200;
                d[i] = t;
            }
            return d;
        }

        public static Rarity Best(List<CardDrop> drops)
        {
            Rarity b = Rarity.Common;
            foreach (CardDrop c in drops)
            {
                Rarity r = Units.Get(c.Job).Rarity;
                if (r > b) b = r;
            }
            return b;
        }
    }
}
