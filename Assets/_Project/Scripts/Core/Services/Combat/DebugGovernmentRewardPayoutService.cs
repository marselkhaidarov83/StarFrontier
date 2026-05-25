using UnityEngine;

public sealed class DebugGovernmentRewardPayoutService : IGovernmentRewardPayoutService
    {
        public void Grant(int credits, int xp)
        {
            Debug.Log($"[GovernmentRewardPayout] Granted credits: {credits}, xp: {xp}");
        }
    }