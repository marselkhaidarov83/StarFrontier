public static class LegacyMigrationReport
{
    public const string Sprint = "Sprint 1";
    public const string Stage = "Stage 2A";

    public static readonly string[] KeepLegacySystems =
    {
            "Legacy combat loop",
            "Legacy NPC systems",
            "Legacy projectile systems",
            "Legacy player movement",
            "Legacy stage 1 scenes",
            "Legacy stage 1 prefabs",
            "Legacy ScriptableObject configs currently used by stage 1"
        };

    public static readonly string[] ExtendLater =
    {
            "Connect legacy combat update to TickService later",
            "Connect legacy events to SimpleEventBus later",
            "Connect legacy configs to ConfigService later",
            "Move legacy runtime data to GameState later",
            "Separate old scene objects from new StarSystemScene gradually"
        };

    public static readonly string[] DoNotTouchInSprint1 =
    {
            "Do not delete old combat scripts",
            "Do not delete old NPC scripts",
            "Do not delete old projectile scripts",
            "Do not modify old prefabs without a specific migration task",
            "Do not move old scenes into new scene flow yet",
            "Do not replace old event logic yet"
        };
}