using System;

namespace DefenseBattle.View
{
    /// <summary>광고 없음 (연결 전): 준비 안 됨</summary>
    public sealed class NoRewardAds : IRewardAds
    {
        public bool IsReady => false;
        public void Show(Action<bool> done) => done?.Invoke(false);
    }
}
