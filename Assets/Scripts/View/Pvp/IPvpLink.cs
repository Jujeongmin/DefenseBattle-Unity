using System;

namespace DefenseBattle.View
{
    /// <summary>
    /// 상대 기기와 직접 잇는 연결 (Relay). 바이트는 PvpWire 형식. 받은 배열은 이벤트 안에서만 유효하다 (재사용 버퍼).
    /// 스냅샷은 늦으면 버려도 되는 방식, 공격은 순서대로 꼭 닿는 방식으로 보낸다.
    /// </summary>
    public interface IPvpLink
    {
        bool Connected { get; }
        event Action<byte[], int> SnapshotReceived;
        event Action<byte[], int> AttackReceived;
        void SendSnapshot(byte[] buf, int len);
        void SendAttack(byte[] buf, int len);
        void Close();
    }
}
