/// <summary>
/// Принятие игроком миссии; сигнал трекеру заданий и представлению.
/// </summary>
public sealed class MissionAcceptedEvent
    {
        /// <summary>Идентификатор миссии в runtime-состоянии.</summary>
        public string MissionRuntimeId;
        /// <summary>Тип миссии.</summary>
        public string MissionType;
    }