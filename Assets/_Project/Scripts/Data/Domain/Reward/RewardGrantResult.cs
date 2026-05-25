using System;
using System.Collections.Generic;

[Serializable]
public class RewardGrantResult
{
    public bool Success;
    public string Message;

    public string SourceId;
    public string SourceType;

    public int CreditsGranted;

    public List<RewardItemEntry> ItemsGranted = new List<RewardItemEntry>();
    public List<RewardItemEntry> ItemsSkipped = new List<RewardItemEntry>();

    public bool CargoWasFull;

    public static RewardGrantResult Failed(string message)
    {
        return new RewardGrantResult
        {
            Success = false,
            Message = message
        };
    }

    public static RewardGrantResult Created(string sourceId, string sourceType)
    {
        return new RewardGrantResult
        {
            Success = true,
            SourceId = sourceId,
            SourceType = sourceType,
            Message = "Reward granted"
        };
    }
}