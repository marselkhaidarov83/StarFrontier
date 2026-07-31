public static class Pseudo3DRenderOrder2
{
    public const string BackgroundLayer = "SystemBackground";
    public const string FarFxLayer = "SystemFarFX";
    public const string ObjectsLayer = "SystemObjects";
    public const string ShipFxLayer = "SystemShipFX";
    public const string ForegroundFxLayer = "SystemForegroundFX";

    public const int BackgroundFar = -1000;
    public const int BackgroundNebula = -900;
    public const int BackgroundStarsNear = -800;

    public const int Sun = -300;
    public const int PlanetBase = 0;
    public const int ExitBase = 300;

    public const int ShipShadow = 700;
    public const int EngineTrail = 720;
    public const int EngineGlow = 730;
    public const int Ship = 750;

    public const int ForegroundDust = 1000;
}
