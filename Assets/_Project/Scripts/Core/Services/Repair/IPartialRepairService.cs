public interface IPartialRepairService : IRepairService
{
    RepairResult Repair(int hullUnits);
}
