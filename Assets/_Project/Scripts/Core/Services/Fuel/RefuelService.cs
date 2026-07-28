using System;

public class RefuelService : IRefuelService
{
    private const int FuelUnitPrice = 10;

    private readonly IGameSessionService _gameSessionService;
    private readonly SimpleEventBus _eventBus;
    private readonly IRefuelCostProvider _refuelCostProvider;
    private readonly IRefuelAvailabilityProvider
        _refuelAvailabilityProvider;

    /// <summary>
    /// Production-конструктор.
    /// Получает зависимости из ServiceRegistry.
    /// </summary>
    public RefuelService()
        : this(
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IGameSessionService>(),

            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>(),

            new RefuelCostProvider(
                FuelUnitPrice),

            new PlanetRefuelAvailabilityProvider())
    {
    }

    /// <summary>
    /// Конструктор с явными зависимостями.
    /// Нужен для автоматических тестов.
    /// </summary>
    public RefuelService(
        IGameSessionService gameSessionService,
        SimpleEventBus eventBus,
        IRefuelCostProvider refuelCostProvider,
        IRefuelAvailabilityProvider
            refuelAvailabilityProvider)
    {
        _gameSessionService =
            gameSessionService ??
            throw new ArgumentNullException(
                nameof(gameSessionService));

        _eventBus =
            eventBus ??
            throw new ArgumentNullException(
                nameof(eventBus));

        _refuelCostProvider =
            refuelCostProvider ??
            throw new ArgumentNullException(
                nameof(refuelCostProvider));

        _refuelAvailabilityProvider =
            refuelAvailabilityProvider ??
            throw new ArgumentNullException(
                nameof(refuelAvailabilityProvider));
    }

    public int GetCurrentFuel()
    {
        if (!TryGetPlayer(
                out PlayerState player))
        {
            return 0;
        }

        if (player.PlayerShipState == null)
            return 0;

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
            return 0;

        return activeShip.CurrentFuel;
    }

    public int GetFuelCapacity()
    {
        if (!TryGetPlayer(
                out PlayerState player))
        {
            return 0;
        }

        if (player.PlayerShipState == null)
            return 0;

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
            return 0;

        return activeShip.FuelCapacity;
    }

    public int GetFuelUnitPrice()
    {
        return _refuelCostProvider
            .GetCostPerFuelUnit();
    }

    public bool CanRefuel(int fuelCount)
    {
        RefuelValidation validation =
            ValidateRefuel(fuelCount);

        return validation.ResultType ==
               RefuelResultType.Success;
    }

    public RefuelResult RefuelToFull()
    {
        if (!TryGetPlayer(
                out PlayerState player))
        {
            return RefuelResult.Create(
                RefuelResultType
                    .MissingPlayerProfile,
                0,
                0,
                0,
                0);
        }

        if (player.PlayerShipState == null)
        {
            return RefuelResult.Create(
                RefuelResultType
                    .MissingPlayerProfile,
                0,
                0,
                0,
                0);
        }

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
        {
            return RefuelResult.Create(
                RefuelResultType
                    .MissingActiveShip,
                0,
                0,
                0,
                0);
        }

        if (activeShip.FuelCapacity <= 0)
        {
            return RefuelResult.Create(
                RefuelResultType
                    .InvalidFuelCapacity,
                0,
                0,
                activeShip.CurrentFuel,
                activeShip.FuelCapacity);
        }

        int missingFuel =
            activeShip.FuelCapacity -
            activeShip.CurrentFuel;

        if (missingFuel <= 0)
        {
            return RefuelResult.Create(
                RefuelResultType
                    .FuelAlreadyFull,
                0,
                0,
                activeShip.CurrentFuel,
                activeShip.FuelCapacity);
        }

        return Refuel(missingFuel);
    }

    public RefuelResult Refuel(int fuelCount)
    {
        RefuelValidation validation =
            ValidateRefuel(fuelCount);

        if (validation.ResultType !=
            RefuelResultType.Success)
        {
            return RefuelResult.Create(
                validation.ResultType,
                0,
                validation.TotalPrice,
                validation.CurrentFuel,
                validation.FuelCapacity);
        }

        if (!TryGetPlayer(
                out PlayerState player))
        {
            return RefuelResult.Create(
                RefuelResultType
                    .MissingPlayerProfile,
                0,
                0,
                0,
                0);
        }

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
        {
            return RefuelResult.Create(
                RefuelResultType
                    .MissingActiveShip,
                0,
                0,
                0,
                0);
        }

        /*
         * Все проверки завершены.
         * Теперь выполняется единое изменение State.
         */

        player.Credits -=
            validation.TotalPrice;

        activeShip.CurrentFuel +=
            fuelCount;

        _eventBus.Publish(
            new FuelChangedEvent(
                activeShip.CurrentFuel));

        _eventBus.Publish(
            new CreditsChangedEvent(
                player.Credits));

        /*
         * Refuel является самостоятельной
         * завершённой транзакцией.
         */
        _eventBus.Publish(
            new SaveNeedEvent());

        return RefuelResult.Create(
            RefuelResultType.Success,
            fuelCount,
            validation.TotalPrice,
            activeShip.CurrentFuel,
            activeShip.FuelCapacity);
    }

    public bool CanConsume(
        int fuelCount,
        out FuelConsumeFailReason reason)
    {
        if (!TryGetPlayer(
                out PlayerState player))
        {
            reason =
                FuelConsumeFailReason
                    .MissingPlayerProfile;

            return false;
        }

        if (player.PlayerShipState == null)
        {
            reason =
                FuelConsumeFailReason
                    .MissingPlayerShipState;

            return false;
        }

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
        {
            reason =
                FuelConsumeFailReason
                    .MissingActiveShip;

            return false;
        }

        if (fuelCount <= 0)
        {
            reason =
                FuelConsumeFailReason
                    .InvalidFuelCount;

            return false;
        }

        if (activeShip.CurrentFuel <
            fuelCount)
        {
            reason =
                FuelConsumeFailReason
                    .NotEnoughFuel;

            return false;
        }

        reason =
            FuelConsumeFailReason.None;

        return true;
    }

    public bool Consume(
        int fuelCount,
        out FuelConsumeFailReason reason)
    {
        if (!CanConsume(
                fuelCount,
                out reason))
        {
            return false;
        }

        if (!TryGetPlayer(
                out PlayerState player))
        {
            reason =
                FuelConsumeFailReason
                    .MissingPlayerProfile;

            return false;
        }

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
        {
            reason =
                FuelConsumeFailReason
                    .MissingActiveShip;

            return false;
        }

        activeShip.CurrentFuel -=
            fuelCount;

        /*
         * При корректной работе CanConsume
         * отрицательного значения быть не должно.
         */
        if (activeShip.CurrentFuel < 0)
            activeShip.CurrentFuel = 0;

        _eventBus.Publish(
            new FuelChangedEvent(
                activeShip.CurrentFuel));

        /*
         * ВАЖНО:
         *
         * Здесь SaveNeedEvent не публикуется.
         *
         * При межсистемном перелёте владельцем
         * полной транзакции является TravelService2A.
         * Он запросит сохранение после изменения:
         *
         * - Fuel;
         * - CurrentSystemId;
         * - Galaxy.CurrentSystemId;
         * - discovery state;
         * - позиции входа.
         */

        reason =
            FuelConsumeFailReason.None;

        return true;
    }

    private RefuelValidation ValidateRefuel(
        int fuelCount)
    {
        if (!TryGetPlayer(
                out PlayerState player))
        {
            return new RefuelValidation(
                RefuelResultType
                    .MissingPlayerProfile,
                0,
                0,
                0);
        }

        if (player.PlayerShipState == null)
        {
            return new RefuelValidation(
                RefuelResultType
                    .MissingPlayerProfile,
                0,
                0,
                0);
        }

        var activeShip =
            player.PlayerShipState
                .GetActiveShip();

        if (activeShip == null)
        {
            return new RefuelValidation(
                RefuelResultType
                    .MissingActiveShip,
                0,
                0,
                0);
        }

        int currentFuel =
            activeShip.CurrentFuel;

        int fuelCapacity =
            activeShip.FuelCapacity;

        if (fuelCount <= 0)
        {
            return new RefuelValidation(
                RefuelResultType
                    .InvalidFuelCount,
                0,
                currentFuel,
                fuelCapacity);
        }

        if (!_refuelAvailabilityProvider
                .IsRefuelAvailable())
        {
            return new RefuelValidation(
                RefuelResultType
                    .RefuelUnavailable,
                0,
                currentFuel,
                fuelCapacity);
        }

        if (fuelCapacity <= 0)
        {
            return new RefuelValidation(
                RefuelResultType
                    .InvalidFuelCapacity,
                0,
                currentFuel,
                fuelCapacity);
        }

        if (currentFuel >= fuelCapacity)
        {
            return new RefuelValidation(
                RefuelResultType
                    .FuelAlreadyFull,
                0,
                currentFuel,
                fuelCapacity);
        }

        int missingFuel =
            fuelCapacity -
            currentFuel;

        if (fuelCount > missingFuel)
        {
            return new RefuelValidation(
                RefuelResultType
                    .FuelCapacityExceeded,
                0,
                currentFuel,
                fuelCapacity);
        }

        int unitPrice =
            _refuelCostProvider
                .GetCostPerFuelUnit();

        if (unitPrice <= 0)
        {
            return new RefuelValidation(
                RefuelResultType
                    .InvalidFuelPrice,
                0,
                currentFuel,
                fuelCapacity);
        }

        long calculatedPrice =
            (long)fuelCount *
            unitPrice;

        if (calculatedPrice >
            int.MaxValue)
        {
            return new RefuelValidation(
                RefuelResultType
                    .InvalidFuelPrice,
                0,
                currentFuel,
                fuelCapacity);
        }

        int totalPrice =
            (int)calculatedPrice;

        if (player.Credits <
            totalPrice)
        {
            return new RefuelValidation(
                RefuelResultType
                    .NotEnoughCredits,
                totalPrice,
                currentFuel,
                fuelCapacity);
        }

        return new RefuelValidation(
            RefuelResultType.Success,
            totalPrice,
            currentFuel,
            fuelCapacity);
    }

    private bool TryGetPlayer(
        out PlayerState player)
    {
        player = null;

        if (_gameSessionService == null)
            return false;

        if (_gameSessionService.State == null)
            return false;

        player =
            _gameSessionService
                .State
                .Player;

        return player != null;
    }

    private readonly struct RefuelValidation
    {
        public RefuelResultType ResultType
        {
            get;
        }

        public int TotalPrice
        {
            get;
        }

        public int CurrentFuel
        {
            get;
        }

        public int FuelCapacity
        {
            get;
        }

        public RefuelValidation(
            RefuelResultType resultType,
            int totalPrice,
            int currentFuel,
            int fuelCapacity)
        {
            ResultType = resultType;
            TotalPrice = totalPrice;
            CurrentFuel = currentFuel;
            FuelCapacity = fuelCapacity;
        }
    }
}