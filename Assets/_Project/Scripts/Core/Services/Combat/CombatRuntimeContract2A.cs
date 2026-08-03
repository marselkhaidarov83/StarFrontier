public enum CombatRuntimeMode2A
{
    SystemScene = 0
}

public static class CombatRuntimeContract2A
{
    public static readonly CombatRuntimeMode2A RuntimeMode =
        CombatRuntimeMode2A.SystemScene;

    public const string CanonicalSceneName =
        "SystemScene";

    public const string CanonicalScenePath =
        "Assets/_Project/Scenes/SystemScene.unity";

    public static readonly string[] RequiredServiceNames =
    {
        "ISystemEncounterService",
        "ISystemEnemyService",
        "ISystemAllyService",
        "ISystemEncounterSaveService",
        "IPlayerCombatTargetService",
        "ISystemNpcCombatService",
        "IGalaxyNpcCombatService",
        "IPlayerAttackService"
    };

    public static readonly string[] RequiredSceneRootNames =
    {
        "EnemiesRoot",
        "AlliesRoot",
        "ProjectilesRoot",
        "VfxRoot"
    };
}