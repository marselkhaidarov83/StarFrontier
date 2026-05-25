public readonly struct RewardGrantedEvent
{
    public readonly RewardGrantResult Result;

    public RewardGrantedEvent(RewardGrantResult result)
    {
        Result = result;
    }
}