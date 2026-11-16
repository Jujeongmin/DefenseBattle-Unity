using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>판 결과 화면에 필요한 서버 응답·기기 정보 (main.ts finishSolo 의 res / reward / meta)</summary>
    public sealed class RunOutcome
    {
        /// <summary>스테이지 보상 (서버가 거부했거나 기록이 없으면 null = 보상 없음)</summary>
        public StageEndResult Stage;
        /// <summary>무한 모드 보석</summary>
        public int Crystals;
        /// <summary>무한 모드 최고 기록 (기기)</summary>
        public int BestWave;
        /// <summary>첫 튜토리얼 판 (1-1, 대전 해금 전)</summary>
        public bool TutorialRun;
        /// <summary>보상형 광고 준비됨 (2배 버튼)</summary>
        public bool AdReady;
    }
}
