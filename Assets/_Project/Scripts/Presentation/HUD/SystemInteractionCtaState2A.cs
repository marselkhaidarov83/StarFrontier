/// <summary>
/// UI-состояние Interaction CTA для SystemScene HUD.
/// Это presentation enum, он не должен хранить gameplay-state.
/// </summary>
public enum SystemInteractionCtaState2A
{
    Available = 0,
    NoTarget = 1,
    OutOfRange = 2,
    Unavailable = 3,
    ServiceMissing = 4,
    Error = 5
}