using System;
using System.Threading.Tasks;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 대전 상대 (pvp.ts PvpSession): 봇이든 실시간 상대(6단계)든 화면 흐름은 이 모양만 안다.
    /// 공격·스냅샷을 주고받고, 끝나면 서버가 정한 결과를 한 번 알린다.
    /// </summary>
    public interface IPvpOpponent
    {
        string Nick { get; }
        int Trophies { get; }
        /// <summary>웨이브 시드 (양쪽 웨이브 구성이 같다)</summary>
        uint Seed { get; }
        /// <summary>상대가 몹을 던짐 → 내 Game.ReceiveAttack</summary>
        event Action<AttackPayload> Attack;
        /// <summary>상대 전장 스냅샷 → 미니맵·VS 줄</summary>
        event Action<BoardSnapshot> Snapshot;
        /// <summary>대전 끝 (승패·트로피·보상) - 한 번만</summary>
        event Action<PvpOutcome> Finished;

        /// <summary>시작 준비 (서버에 대전 시작 기록) - 실패해도 대전은 진행</summary>
        Task Begin();
        /// <summary>카운트다운이 끝난 뒤 매 프레임 (실제 경과 초)</summary>
        void Tick(double realDt);
        void SendAttack(AttackPayload p);
        void SendSnapshot(BoardSnapshot s);
        /// <summary>내 전장이 무너짐 (또는 나가기)</summary>
        void ReportDefeat();
        /// <summary>대전을 내림 - 더 진행하지 않는다</summary>
        void Leave();
    }
}
