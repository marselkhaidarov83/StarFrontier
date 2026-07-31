/// <summary>
/// Чистая utility-логика для текстов Interaction CTA.
/// Не читает сцену, не меняет UI и не хранит gameplay-state.
/// </summary>
public static class SystemInteractionCtaTextUtility2A
{
    public const string AvailableButtonText = "Действие";
    public const string NoTargetButtonText = "Нет цели";
    public const string OutOfRangeButtonText = "Далеко";
    public const string UnavailableButtonText = "Недоступно";
    public const string ServiceMissingButtonText = "Нет сервиса";
    public const string ErrorButtonText = "Ошибка";

    public const string AvailableReasonText = "";
    public const string NoTargetReasonText = "Выберите цель";
    public const string OutOfRangeReasonText = "Подлетите ближе";
    public const string UnavailableReasonText = "Действие недоступно";
    public const string ServiceMissingReasonText = "InteractionService не найден";
    public const string ErrorReasonText = "Ошибка действия";

    public static string GetButtonText(
        SystemInteractionCtaState2A state)
    {
        switch (state)
        {
            case SystemInteractionCtaState2A.Available:
                return AvailableButtonText;

            case SystemInteractionCtaState2A.NoTarget:
                return NoTargetButtonText;

            case SystemInteractionCtaState2A.OutOfRange:
                return OutOfRangeButtonText;

            case SystemInteractionCtaState2A.ServiceMissing:
                return ServiceMissingButtonText;

            case SystemInteractionCtaState2A.Error:
                return ErrorButtonText;

            case SystemInteractionCtaState2A.Unavailable:
            default:
                return UnavailableButtonText;
        }
    }

    public static string GetReasonText(
        SystemInteractionCtaState2A state)
    {
        switch (state)
        {
            case SystemInteractionCtaState2A.Available:
                return AvailableReasonText;

            case SystemInteractionCtaState2A.NoTarget:
                return NoTargetReasonText;

            case SystemInteractionCtaState2A.OutOfRange:
                return OutOfRangeReasonText;

            case SystemInteractionCtaState2A.ServiceMissing:
                return ServiceMissingReasonText;

            case SystemInteractionCtaState2A.Error:
                return ErrorReasonText;

            case SystemInteractionCtaState2A.Unavailable:
            default:
                return UnavailableReasonText;
        }
    }

    public static SystemInteractionCtaState2A FromFailReason(
        object failReason)
    {
        if (failReason == null)
            return SystemInteractionCtaState2A.Unavailable;

        string value =
            failReason
                .ToString()
                .Trim()
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(value))
            return SystemInteractionCtaState2A.Unavailable;

        if (value == "none" ||
            value == "available" ||
            value == "ok" ||
            value == "success")
        {
            return SystemInteractionCtaState2A.Available;
        }

        if (value.Contains("notarget") ||
            value.Contains("no_target") ||
            value.Contains("no target") ||
            value.Contains("targetmissing") ||
            value.Contains("target_missing"))
        {
            return SystemInteractionCtaState2A.NoTarget;
        }

        if (value.Contains("range") ||
            value.Contains("distance") ||
            value.Contains("far"))
        {
            return SystemInteractionCtaState2A.OutOfRange;
        }

        return SystemInteractionCtaState2A.Unavailable;
    }
}