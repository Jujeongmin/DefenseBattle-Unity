using System;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>에디터용: 광고를 다 본 것으로 바로 처리</summary>
    public sealed class EditorRewardAds : IRewardAds
    {
        public bool IsReady => true;

        public void Show(Action<bool> done)
        {
            Debug.Log("[RewardAds] 에디터 - 광고 시청 완료로 처리");
            done?.Invoke(true);
        }
    }
}
