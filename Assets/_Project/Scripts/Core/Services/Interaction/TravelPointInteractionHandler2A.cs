using System;

/// <summary>
/// Выполняет взаимодействие с точкой
/// межсистемного перехода.
///
/// TargetId должен содержать ID системы назначения.
/// Сам handler не изменяет Fuel, Save или CurrentSystemId.
/// Это делает TravelService2A.
/// </summary>
public sealed class TravelPointInteractionHandler2A :
    IInteractionHandler2A
{
    private readonly ITravelService
        _travelService;

    private readonly IGameSessionService
        _gameSessionService;

    public TravelPointInteractionHandler2A(
        ITravelService travelService,
        IGameSessionService gameSessionService)
    {
        _travelService =
            travelService ??
            throw new ArgumentNullException(
                nameof(travelService));

        _gameSessionService =
            gameSessionService ??
            throw new ArgumentNullException(
                nameof(gameSessionService));
    }

    public bool CanHandle(
        SystemGameplayTargetType targetType)
    {
        string typeName =
            targetType.ToString();

        return
            ContainsIgnoreCase(
                typeName,
                "travel") ||
            ContainsIgnoreCase(
                typeName,
                "exit") ||
            ContainsIgnoreCase(
                typeName,
                "route");
    }

    public InteractionDescriptor2A
        CreateDescriptor(
            string targetId,
            SystemGameplayTargetType targetType)
    {
        return new InteractionDescriptor2A(
            targetId,
            targetType,
            "interstellar_travel",
            "Прыгнуть в систему",
            "ui_interaction_primary_01");
    }

    public InteractionFailReason2A
        GetFailReason(
            InteractionDescriptor2A descriptor)
    {
        if (descriptor == null ||
            string.IsNullOrWhiteSpace(
                descriptor.TargetId))
        {
            return
                InteractionFailReason2A
                    .TargetUnavailable;
        }

        if (_gameSessionService.State == null ||
            _gameSessionService
                .State
                .Player == null)
        {
            return
                InteractionFailReason2A
                    .ServiceNotReady;
        }

        string currentSystemId =
            _gameSessionService
                .State
                .Player
                .CurrentSystemId;

        if (string.IsNullOrWhiteSpace(
                currentSystemId))
        {
            return
                InteractionFailReason2A
                    .RequirementsNotMet;
        }

        TravelFailReason travelReason =
            _travelService
                .GetTravelFailReason(
                    currentSystemId,
                    descriptor.TargetId);

        return MapTravelFailReason(
            travelReason);
    }

    public InteractionExecutionResult2A
        Execute(
            InteractionDescriptor2A descriptor)
    {
        InteractionFailReason2A failReason =
            GetFailReason(
                descriptor);

        if (failReason !=
            InteractionFailReason2A.None)
        {
            return
                InteractionExecutionResult2A
                    .Failed(
                        failReason,
                        descriptor,
                        GetMessage(
                            failReason));
        }

        TravelResult travelResult =
            _travelService
                .TryTravel(
                    descriptor.TargetId);

        if (travelResult == null)
        {
            return
                InteractionExecutionResult2A
                    .Failed(
                        InteractionFailReason2A
                            .ExecutionFailed,
                        descriptor,
                        "Перелёт не выполнен");
        }

        if (!travelResult.Success)
        {
            InteractionFailReason2A
                mappedReason =
                    MapTravelFailReason(
                        travelResult
                            .FailReason);

            return
                InteractionExecutionResult2A
                    .Failed(
                        mappedReason,
                        descriptor,
                        GetMessage(
                            mappedReason));
        }

        return
            InteractionExecutionResult2A
                .Completed(
                    descriptor,
                    "Межсистемный переход выполнен");
    }

    private static InteractionFailReason2A
        MapTravelFailReason(
            TravelFailReason reason)
    {
        if (reason ==
            TravelFailReason.None)
        {
            return
                InteractionFailReason2A.None;
        }

        if (reason ==
            TravelFailReason.NotEnoughFuel)
        {
            return
                InteractionFailReason2A
                    .InsufficientFuel;
        }

        return
            InteractionFailReason2A
                .RequirementsNotMet;
    }

    private static string GetMessage(
        InteractionFailReason2A reason)
    {
        switch (reason)
        {
            case InteractionFailReason2A
                .InsufficientFuel:
                return
                    "Недостаточно топлива";

            case InteractionFailReason2A
                .TargetUnavailable:
                return
                    "Точка перехода недоступна";

            case InteractionFailReason2A
                .ServiceNotReady:
                return
                    "Сервис перелёта недоступен";

            case InteractionFailReason2A
                .RequirementsNotMet:
                return
                    "Межсистемный переход невозможен";

            default:
                return
                    "Перелёт не выполнен";
        }
    }

    private static bool ContainsIgnoreCase(
        string source,
        string value)
    {
        if (string.IsNullOrEmpty(source) ||
            string.IsNullOrEmpty(value))
        {
            return false;
        }

        return
            source.IndexOf(
                value,
                StringComparison
                    .OrdinalIgnoreCase) >= 0;
    }
}