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

    public Sprite ForMission(MissionInstanceData mission)
    {
        if (useNeutralAsFixedBackground && neutral != null) return neutral;
        if (mission == null) return neutral;
        switch (mission.MissionType)
        {
            case MissionType.Delivery: return delivery != null ? delivery : neutral;
            case MissionType.Recon: return recon != null ? recon : neutral;
            case MissionType.Defense: return defense != null ? defense : neutral;
            case MissionType.Liberation: return liberation != null ? liberation : neutral;
            case MissionType.RouteSearch:
                return mission.Objective != null && mission.Objective.RouteIsUpgrade
                    ? (routeImprove != null ? routeImprove : neutral)
                    : (routeUnlock != null ? routeUnlock : neutral);
            default: return neutral;
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

    public Sprite Get(PlanetType type, MissionInstanceData mission)
    {
        if (planets == null) return null;
        for (int i = 0; i < planets.Length; i++)
            if (planets[i] != null && planets[i].planetType == type)
                return planets[i].ForMission(mission);
        return null;
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
