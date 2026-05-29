using System;
using System.Collections.Generic;
using System.Linq;

    [Serializable]
    public sealed class SystemPopulationRuntimeState
    {
        public List<SystemPopulationRuleTimerState> Timers = new();

        public SystemPopulationRuleTimerState GetOrCreateTimer(string systemId, string ruleId)
        {
            SystemPopulationRuleTimerState timer = Timers.FirstOrDefault(
                x => x.SystemId == systemId && x.RuleId == ruleId
            );

            if (timer != null)
                return timer;

            timer = new SystemPopulationRuleTimerState(systemId, ruleId);
            Timers.Add(timer);

            return timer;
        }

        public void Clear()
        {
            Timers.Clear();
        }
    }