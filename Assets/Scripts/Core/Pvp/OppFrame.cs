namespace DefenseBattle.Core
{
    /// <summary>
    /// 상대 전장 한 장면 (SnapshotTimeline.Sample 결과): 보간된 몹 위치 + 최신 스냅샷의 유닛·빈틈·요약.
    /// 배열은 처음에 최대 크기로 만들어 두고 매 프레임 다시 채운다 (할당 없음). 좌표는 전장 논리 px (0~MapSize).
    /// </summary>
    public sealed class OppFrame
    {
        public const int MaxMobs = 120, MaxUnits = 80, MaxDeck = 8;

        public int MobCount;
        public readonly int[] MobId = new int[MaxMobs];
        /// <summary>스냅샷 code: 종족 | 종류&lt;&lt;2 | 방향&lt;&lt;4 | 그림변형&lt;&lt;6</summary>
        public readonly int[] MobCode = new int[MaxMobs];
        public readonly int[] MobSize = new int[MaxMobs];
        /// <summary>남은 체력 % 0~100</summary>
        public readonly int[] MobHp = new int[MaxMobs];
        public readonly int[] MobStatus = new int[MaxMobs];
        public readonly float[] MobX = new float[MaxMobs];
        public readonly float[] MobY = new float[MaxMobs];

        public int UnitCount;
        public readonly int[] UnitJob = new int[MaxUnits];
        public readonly int[] UnitGrade = new int[MaxUnits];
        /// <summary>방향 | 공격 중 4</summary>
        public readonly int[] UnitFace = new int[MaxUnits];
        public readonly float[] UnitX = new float[MaxUnits];
        public readonly float[] UnitY = new float[MaxUnits];

        /// <summary>길 표본마다 사거리 빈틈 (BoardSnapshot.GapSamples 개)</summary>
        public readonly bool[] Gap = new bool[BoardSnapshot.GapSamples];

        public int Wave;
        /// <summary>필드 몹 수 (먼저 Waves.GameOverMobCount 가 되면 패배)</summary>
        public int Mobs;
        public int Units;
        /// <summary>보스 체력 % (없으면 -1)</summary>
        public int BossPct = -1;
        public int DeckCount, AtkCount;
        public readonly int[] Deck = new int[MaxDeck];
        public readonly int[] Atk = new int[MaxDeck];

        public static int Race(int code) => code & 3;
        public static int Kind(int code) => (code >> 2) & 3;
        public static int Dir(int code) => (code >> 4) & 3;
        public static int Variant(int code) => code >> 6;
    }
}
