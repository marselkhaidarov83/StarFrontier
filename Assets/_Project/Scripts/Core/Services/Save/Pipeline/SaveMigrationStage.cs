public sealed class SaveMigrationStage
{
    public bool Run(GameRuntimeState state)
    {
        return SaveMigrationService.Migrate(state);
    }
}
