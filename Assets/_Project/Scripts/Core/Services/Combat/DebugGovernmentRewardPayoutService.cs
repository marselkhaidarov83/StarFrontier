using UnityEngine;

public sealed class DebugGovernmentRewardPayoutService : IGovernmentRewardPayoutService
{
    public void Grant(int credits, int xp)
    {
        AppLog.Info(DebugLogChannel.Combat, $"[GovernmentRewardPayout] Granted credits: {credits}, xp: {xp}");
    }
}