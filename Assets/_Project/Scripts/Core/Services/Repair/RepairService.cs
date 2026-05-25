
public class RepairService : IRepairService
    {
        private readonly IGameSessionService _gameSessionService;

        private const int RepairUnitPrice = 2;

        public RepairService()
        {
            _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        }

        public int GetRepairUnitPrice()
        {
            return RepairUnitPrice;
        }

        public RepairResult RepairToFull()
        {
            PlayerProfileData playerProfile = _gameSessionService.CurrentSave.PlayerProfile;
            if (playerProfile == null)
            {
                return RepairResult.Create(
                    RepairResultType.MissingPlayerProfile,
                    0,
                    0,
                    0,
                    0);
            }

            if (playerProfile.PlayerShipState.GetActiveShip().HullCapacity <= 0)
            {
                return RepairResult.Create(
                    RepairResultType.InvalidHullCapacity,
                    0,
                    0,
                    playerProfile.PlayerShipState.GetActiveShip().CurrentHull,
                    playerProfile.PlayerShipState.GetActiveShip().HullCapacity);
            }

            // if (RepairUnitPrice <= 0)
            // {
            //     return RepairResult.Create(
            //         RepairResultType.InvalidRepairPrice,
            //         0,
            //         0,
            //         playerProfile.currentHull,
            //         playerProfile.hullCapacity);
            // }

            if (playerProfile.PlayerShipState.GetActiveShip().CurrentHull >= playerProfile.PlayerShipState.GetActiveShip().HullCapacity)
            {
                return RepairResult.Create(
                    RepairResultType.HullAlreadyFull,
                    0,
                    0,
                    playerProfile.PlayerShipState.GetActiveShip().CurrentHull,
                    playerProfile.PlayerShipState.GetActiveShip().HullCapacity);
            }

            int missingHull = playerProfile.PlayerShipState.GetActiveShip().HullCapacity - playerProfile.PlayerShipState.GetActiveShip().CurrentHull;
            int totalPrice = missingHull * RepairUnitPrice;

            if (playerProfile.Credits < totalPrice)
            {
                return RepairResult.Create(
                    RepairResultType.NotEnoughCredits,
                    0,
                    totalPrice,
                    playerProfile.PlayerShipState.GetActiveShip().CurrentHull,
                    playerProfile.PlayerShipState.GetActiveShip().HullCapacity);
            }

            playerProfile.Credits -= totalPrice;
            playerProfile.PlayerShipState.GetActiveShip().CurrentHull = playerProfile.PlayerShipState.GetActiveShip().HullCapacity;

            return RepairResult.Create(
                RepairResultType.Success,
                missingHull,
                totalPrice,
                playerProfile.PlayerShipState.GetActiveShip().CurrentHull,
                playerProfile.PlayerShipState.GetActiveShip().HullCapacity);
        }
    }