public static class StationAttackRules2A
{
    public static bool TryGetStationType(SystemNpcBehaviorType behavior, out StationType type)
    {
        switch (behavior)
        {
            case SystemNpcBehaviorType.AttackMilitaryStation: type = StationType.Military; return true;
            case SystemNpcBehaviorType.AttackRangerBaseStation: type = StationType.RangerBase; return true;
            case SystemNpcBehaviorType.AttackTradeStation: type = StationType.Trade; return true;
            case SystemNpcBehaviorType.AttackScienceStation: type = StationType.Science; return true;
            case SystemNpcBehaviorType.AttackMedicalStation: type = StationType.Medical; return true;
            default: type = default; return false;
        }
    }
    public static bool IsAttack(SystemNpcBehaviorType behavior) => TryGetStationType(behavior, out _);
    public static bool CanTarget(StationRuntimeState station) => station != null && station.Level > 0 &&
        station.IsOperational && !station.ConfigMissing && station.Recovery == null && station.Health > 0;
}
