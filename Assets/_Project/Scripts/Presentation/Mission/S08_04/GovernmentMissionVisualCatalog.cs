using System;
using UnityEngine;

[Serializable]
public sealed class GovernmentMissionVisualSet
{
    public PlanetType planetType;
    public Sprite neutral;
    public Sprite delivery;
    public Sprite recon;
    public Sprite defense;
    public Sprite liberation;
    public Sprite routeUnlock;
    public Sprite routeImprove;
    public bool useNeutralAsFixedBackground;
    public Sprite deliveryOverlay;
    public Sprite reconOverlay;
    public Sprite defenseOverlay;
    public Sprite liberationOverlay;
    public Sprite routeUnlockOverlay;
    public Sprite routeImproveOverlay;

    public Sprite ForMission(MissionInstanceData mission, Sprite baseIllustration = null)
    {
        Sprite background = baseIllustration != null ? baseIllustration : neutral;
        if (useNeutralAsFixedBackground && background != null) return background;
        if (mission == null) return background;
        switch (mission.MissionType)
        {
            case MissionType.Delivery: return delivery != null ? delivery : background;
            case MissionType.Recon: return recon != null ? recon : background;
            case MissionType.Defense: return defense != null ? defense : background;
            case MissionType.Liberation: return liberation != null ? liberation : background;
            case MissionType.RouteSearch:
                return mission.Objective != null && mission.Objective.RouteIsUpgrade
                    ? (routeImprove != null ? routeImprove : background)
                    : (routeUnlock != null ? routeUnlock : background);
            default: return background;
        }
    }

    public Sprite OverlayForMission(MissionInstanceData mission)
    {
        if (!useNeutralAsFixedBackground || mission == null) return null;
        switch (mission.MissionType)
        {
            case MissionType.Delivery: return deliveryOverlay;
            case MissionType.Recon: return reconOverlay;
            case MissionType.Defense: return defenseOverlay;
            case MissionType.Liberation: return liberationOverlay;
            case MissionType.RouteSearch:
                return mission.Objective != null && mission.Objective.RouteIsUpgrade
                    ? routeImproveOverlay
                    : routeUnlockOverlay;
            default: return null;
        }
    }
}

[CreateAssetMenu(fileName = "GovernmentMissionVisualCatalog",
    menuName = "StarFrontier/Configs/Mission/Government Visual Catalog")]
public sealed class GovernmentMissionVisualCatalog : ScriptableObject
{
    [SerializeField] private GovernmentMissionVisualSet[] planets;

    public Sprite Get(PlanetType type, MissionInstanceData mission, Sprite baseIllustration = null)
    {
        if (planets == null) return baseIllustration;
        for (int i = 0; i < planets.Length; i++)
            if (planets[i] != null && planets[i].planetType == type)
                return planets[i].ForMission(mission, baseIllustration);
        return baseIllustration;
    }

    public Sprite GetOverlay(PlanetType type, MissionInstanceData mission)
    {
        if (planets == null) return null;
        for (int i = 0; i < planets.Length; i++)
            if (planets[i] != null && planets[i].planetType == type)
                return planets[i].OverlayForMission(mission);
        return null;
    }
}
