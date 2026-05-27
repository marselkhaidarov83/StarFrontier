using System;

    [Serializable]
    public sealed class SystemPopulationRuleTimerState
    {
        public string SystemId;
        public string RuleId;

        public float TimerSeconds;

        public SystemPopulationRuleTimerState(string systemId, string ruleId)
        {
            SystemId = systemId;
            RuleId = ruleId;
            TimerSeconds = 0f;
        }
    }