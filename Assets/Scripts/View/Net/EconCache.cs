using System;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>서버가 보낸 경제 상태의 사본 (원본 econ.ts applyEcon). 화면은 읽기만 하고, 바뀌면 Changed 로 다시 그린다</summary>
    public static class EconCache
    {
        /// <summary>마지막으로 받은 상태 (접속 전에는 새 계정 모양)</summary>
        public static EconState State { get; private set; } = EconState.Fresh("S0000-00");

        /// <summary>한 번이라도 서버에서 받았나</summary>
        public static bool Loaded { get; private set; }

        public static event Action Changed;

        public static void Apply(EconState e)
        {
            if (e == null) return;
            State = e;
            Loaded = true;
            Changed?.Invoke();
        }
    }
}
