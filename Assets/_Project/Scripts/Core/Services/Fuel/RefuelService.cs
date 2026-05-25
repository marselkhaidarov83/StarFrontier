    public class RefuelService : IRefuelService
    {
        private readonly IGameSessionService _gameSessionService;
        private readonly SimpleEventBus eventBus;
        private readonly IRefuelCostProvider refuelCostProvider;

        private const int FuelUnitPrice = 10;

        public RefuelService()
        {
            _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
            eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            refuelCostProvider = new RefuelCostProvider(FuelUnitPrice);
        }

        public int GetFuelUnitPrice()
        {
            return refuelCostProvider.GetCostPerFuelUnit();
        }

        public bool CanRefuel(int fuelCount)
        {
            // if (!_availabilityProvider.IsRefuelAvailable())
            //     return false;

            PlayerProfileData player = _gameSessionService.CurrentSave.PlayerProfile;

            if (player.PlayerShipState.GetActiveShip().CurrentFuel + fuelCount >
                        player.PlayerShipState.GetActiveShip().FuelCapacity)
                return false;

            int cost = refuelCostProvider.GetCostPerFuelUnit();
            if (player.Credits < cost)
                return false;

            return true;
        }

        public RefuelResult RefuelToFull()
        {
            PlayerProfileData playerProfile = _gameSessionService.CurrentSave.PlayerProfile;
            int missingFuel = playerProfile.PlayerShipState.GetActiveShip().FuelCapacity - playerProfile.PlayerShipState.GetActiveShip().CurrentFuel;

            return Refuel(missingFuel);

            // if (playerProfile == null)
            // {
            //     return RefuelResult.Create(
            //         RefuelResultType.MissingPlayerProfile,
            //         0,
            //         0,
            //         0,
            //         0);
            // }

            // if (playerProfile.CurrentShipRuntimeData.FuelCapacity <= 0)
            // {
            //     return RefuelResult.Create(
            //         RefuelResultType.InvalidFuelCapacity,
            //         0,
            //         0,
            //         playerProfile.CurrentShipRuntimeData.CurrentFuel,
            //         playerProfile.CurrentShipRuntimeData.FuelCapacity);
            // }

            // // if (FuelUnitPrice <= 0)
            // // {
            // //     return RefuelResult.Create(
            // //         RefuelResultType.InvalidFuelPrice,
            // //         0,
            // //         0,
            // //         playerProfile.CurrentShipRuntimeData.CurrentFuel,
            // //         playerProfile.CurrentShipRuntimeData.FuelCapacity);
            // // }

            // if (playerProfile.CurrentShipRuntimeData.CurrentFuel >= playerProfile.CurrentShipRuntimeData.FuelCapacity)
            // {
            //     return RefuelResult.Create(
            //         RefuelResultType.FuelAlreadyFull,
            //         0,
            //         0,
            //         playerProfile.CurrentShipRuntimeData.CurrentFuel,
            //         playerProfile.CurrentShipRuntimeData.FuelCapacity);
            // }

            // int missingFuel = playerProfile.CurrentShipRuntimeData.FuelCapacity - playerProfile.CurrentShipRuntimeData.CurrentFuel;
            // int totalPrice = missingFuel * refuelCostProvider.GetCostPerFuelUnit();

            // if (playerProfile.Credits < totalPrice)
            // {
            //     return RefuelResult.Create(
            //         RefuelResultType.NotEnoughCredits,
            //         0,
            //         totalPrice,
            //         playerProfile.CurrentShipRuntimeData.CurrentFuel,
            //         playerProfile.CurrentShipRuntimeData.FuelCapacity);
            // }

            // playerProfile.Credits -= totalPrice;
            // playerProfile.CurrentShipRuntimeData.CurrentFuel = playerProfile.CurrentShipRuntimeData.FuelCapacity;

            // eventBus.Publish(new FuelChangedEvent(playerProfile.CurrentShipRuntimeData.CurrentFuel)); 
            // return RefuelResult.Create(
            //     RefuelResultType.Success,
            //     missingFuel,
            //     totalPrice,
            //     playerProfile.CurrentShipRuntimeData.CurrentFuel,
            //     playerProfile.CurrentShipRuntimeData.FuelCapacity);
        }

    public RefuelResult Refuel(int fuelCount)
        {
            PlayerProfileData playerProfile = _gameSessionService.CurrentSave.PlayerProfile;
            if (playerProfile == null)
            {
                return RefuelResult.Create(
                    RefuelResultType.MissingPlayerProfile,
                    0,
                    0,
                    0,
                    0);
            }

            if (playerProfile.PlayerShipState.GetActiveShip().FuelCapacity <= 0)
            {
                return RefuelResult.Create(
                    RefuelResultType.InvalidFuelCapacity,
                    0,
                    0,
                    playerProfile.PlayerShipState.GetActiveShip().CurrentFuel,
                    playerProfile.PlayerShipState.GetActiveShip().FuelCapacity);
            }

            // if (FuelUnitPrice <= 0)
            // {
            //     return RefuelResult.Create(
            //         RefuelResultType.InvalidFuelPrice,
            //         0,
            //         0,
            //         playerProfile.CurrentShipRuntimeData.CurrentFuel,
            //         playerProfile.CurrentShipRuntimeData.FuelCapacity);
            // }

            if (playerProfile.PlayerShipState.GetActiveShip().CurrentFuel >= playerProfile.PlayerShipState.GetActiveShip().FuelCapacity)
            {
                return RefuelResult.Create(
                    RefuelResultType.FuelAlreadyFull,
                    0,
                    0,
                    playerProfile.PlayerShipState.GetActiveShip().CurrentFuel,
                    playerProfile.PlayerShipState.GetActiveShip().FuelCapacity);
            }

            int totalPrice = fuelCount * refuelCostProvider.GetCostPerFuelUnit();

            if (playerProfile.Credits < totalPrice)
            {
                return RefuelResult.Create(
                    RefuelResultType.NotEnoughCredits,
                    0,
                    totalPrice,
                    playerProfile.PlayerShipState.GetActiveShip().CurrentFuel,
                    playerProfile.PlayerShipState.GetActiveShip().FuelCapacity);
            }

            playerProfile.Credits -= totalPrice;
            playerProfile.PlayerShipState.GetActiveShip().CurrentFuel += fuelCount;

            eventBus.Publish(new FuelChangedEvent(playerProfile.PlayerShipState.GetActiveShip().CurrentFuel)); 
            eventBus.Publish(new CreditsChangedEvent(playerProfile.Credits)); 

            eventBus.Publish(new SaveNeedEvent());

            return RefuelResult.Create(
                RefuelResultType.Success,
                fuelCount,
                totalPrice,
                playerProfile.PlayerShipState.GetActiveShip().CurrentFuel,
                playerProfile.PlayerShipState.GetActiveShip().FuelCapacity);
        }        
    }