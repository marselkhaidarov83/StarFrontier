using System;
using UnityEngine;

[Serializable]
public sealed class GovernmentMissionVisualSet
{
    public PlanetType planetType;
    public Sprite delivery;
    public Sprite recon;
    public Sprite defense;
    public Sprite liberation;
    public Sprite routeUnlock;
    public Sprite routeImprove;
    [Tooltip("Показывать иллюстрацию миссии поверх основного фона типа планеты.")]
    public bool useMissionOverlay;

    public Sprite ForMission(MissionInstanceData mission, Sprite baseIllustration)
    {
        if (useMissionOverlay) return baseIllustration;
        Sprite illustration = GetMissionSprite(mission);
        return illustration != null ? illustration : baseIllustration;
    }

    public Sprite OverlayForMission(MissionInstanceData mission) =>
        useMissionOverlay ? GetMissionSprite(mission) : null;

    private Sprite GetMissionSprite(MissionInstanceData mission)
    {
        if (mission == null) return null;
        switch (mission.MissionType)
        {
            case MissionType.Delivery: return delivery;
            case MissionType.Recon: return recon;
            case MissionType.Defense: return defense;
            case MissionType.Liberation: return liberation;
            case MissionType.RouteSearch:
                return mission.Objective != null && mission.Objective.RouteIsUpgrade
                    ? routeImprove
                    : routeUnlock;
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
