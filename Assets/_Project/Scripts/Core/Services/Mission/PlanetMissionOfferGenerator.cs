using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlanetMissionOfferGenerator : CustomService, IPlanetMissionOfferGenerator
{
    public PlanetMissionOfferGenerator()
    {
        _debugStop = true;
    }

    public PlanetOfferedMissionData TryGenerateOffer(
        StarSystemConfig starSystem,
        PlanetConfig planetConfig,
        IReadOnlyList<MissionInstanceData> playerMissions,
        PlanetOfferedMissionData existingOffer)
    {
        if (planetConfig == null)
        {
            Debug.LogWarning("PlanetMissionOfferGenerator: planetConfig is null.");
            return null;
        }

        if (existingOffer != null && existingOffer.OfferedMission != null)
        {
            Debug.Log($"PlanetMissionOfferGenerator: Existing offer reused for planet {planetConfig.Id}");
            return existingOffer;
        }

        bool alreadyHasMissionFromPlanet = playerMissions != null &&
                                           playerMissions.Any(m => m != null && m.SourcePlanetId == planetConfig.Id);

        if (alreadyHasMissionFromPlanet)
        {
            Debug.Log($"PlanetMissionOfferGenerator: Player already has mission from planet {planetConfig.Id}");
            return null;
        }

        List<PlanetMissionOfferEntryConfig> validEntries = planetConfig.PlanetMissionConfig.PossibleMissionOffers
            .Where(e => e != null && e.IsEnabled && e.Weight > 0 && !string.IsNullOrWhiteSpace(e.MissionTemplateConfig.Id))
            .ToList();

        if (validEntries.Count == 0)
        {
            Debug.LogWarning($"PlanetMissionOfferGenerator: No valid entries for planet {planetConfig.Id}");
            return null;
        }

        PlanetMissionOfferEntryConfig selectedEntry = PickByWeight(validEntries);
        MissionInstanceData mission = BuildMissionFromTemplateId(starSystem, planetConfig, selectedEntry.MissionTemplateConfig);

        if (mission == null)
        {
            Debug.LogWarning($"PlanetMissionOfferGenerator: Failed to build mission from template {selectedEntry.MissionTemplateConfig.Id}");
            return null;
        }

        return new PlanetOfferedMissionData
        {
            PlanetId = planetConfig.Id,
            OfferedMission = mission
        };
    }

    private PlanetMissionOfferEntryConfig PickByWeight(List<PlanetMissionOfferEntryConfig> entries)
    {
        int totalWeight = entries.Sum(e => e.Weight);
        int roll = UnityEngine.Random.Range(0, totalWeight);

        int current = 0;
        foreach (PlanetMissionOfferEntryConfig entry in entries)
        {
            current += entry.Weight;
            if (roll < current)
                return entry;
        }

        return entries[0];
    }

    private MissionInstanceData BuildMissionFromTemplateId(
            StarSystemConfig starSystem, PlanetConfig planet, MissionTemplateConfig missionTemplate)
    {
        return missionTemplate.Id switch
        {
            "mission_delivery_01" => BuildDeliveryMission(starSystem, planet, missionTemplate),
            "mission_elimination_01" => BuildEliminationMission(starSystem, planet, missionTemplate),
            "mission_recon_01" => BuildReconMission(starSystem, planet, missionTemplate),
            "mission_delivery_basic_01" => BuildDeliveryMission(starSystem, planet, missionTemplate),
            "mission_delivery_basic_02" => BuildDeliveryMission(starSystem, planet, missionTemplate),
            "mission_elimination_pirates_01" => BuildEliminationMission(starSystem, planet, missionTemplate),
            "mission_elimination_pirates_02" => BuildEliminationMission(starSystem, planet, missionTemplate),
            "mission_recon_jump_point_01" => BuildReconMission(starSystem, planet, missionTemplate),
            "mission_recon_jump_point_02" => BuildReconMission(starSystem, planet, missionTemplate),
            _ => null
        };
    }

    private MissionInstanceData BuildDeliveryMission(StarSystemConfig starSystem, PlanetConfig planet, MissionTemplateConfig missionTemplate)
    {
        LogCustom("starSystem = " + starSystem);
        LogCustom("planet = " + planet);
        LogCustom("missionTemplate = " + missionTemplate);
        LogCustom("missionTemplate.Id = " + missionTemplate.Id);
        LogCustom("missionTemplate.DisplayName = " + missionTemplate.DisplayName);
        LogCustom("missionTemplate.Description = " + missionTemplate.Description);
        LogCustom("planet.Id = " + planet.Id);
        LogCustom("starSystem.Id = " + starSystem.Id);

        return new MissionInstanceData
        {
            MissionRuntimeId = $"mission_runtime_delivery_{Guid.NewGuid():N}",
            TemplateId = missionTemplate.Id,
            Title = missionTemplate.DisplayName,
            Description = missionTemplate.Description,
            MissionType = MissionType.Delivery,
            Status = MissionStatus.Available,
            SourcePlanetId = planet.Id,
            SourceSystemId = starSystem.Id,
            TargetPlanetId = missionTemplate.TargetPlanet.Id,
            TargetSystemId = missionTemplate.TargetSystem.Id,
            Reward = new MissionRewardData
            {
                Credits = Random.Range(missionTemplate.CreditRewardMin, missionTemplate.CreditRewardMax + 1),
                Xp = Random.Range(missionTemplate.XpRewardMin, missionTemplate.XpRewardMax + 1)
            },
            Objective = new MissionObjectiveData
            {
                MissionCargoItemId = "item_medicine_01",
                MissionCargoAmount = 5,
                RequiredAmount = 1,
                CurrentAmount = 0
            }
        };
    }

    private MissionInstanceData BuildEliminationMission(StarSystemConfig starSystem, PlanetConfig planet, MissionTemplateConfig missionTemplate)
    {
        // LogCustom("starSystem = " + starSystem);
        // LogCustom("planet = " + planet);
        // LogCustom("missionTemplate = " + missionTemplate);
        // LogCustom("missionTemplate.Id = " + missionTemplate.Id);
        // LogCustom("missionTemplate.DisplayName = " + missionTemplate.DisplayName);
        // LogCustom("missionTemplate.Description = " + missionTemplate.Description);
        // LogCustom("planet.Id = " + planet.Id);
        // LogCustom("starSystem.Id = " + starSystem.Id);

        return new MissionInstanceData
        {
            MissionRuntimeId = $"mission_runtime_elimination_{Guid.NewGuid():N}",
            TemplateId = missionTemplate.Id,
            Title = missionTemplate.DisplayName,
            Description = missionTemplate.Description,
            MissionType = MissionType.Elimination,
            Status = MissionStatus.Available,
            SourcePlanetId = planet.Id,
            SourceSystemId = starSystem.Id,
            TargetPlanetId = planet.Id,
            TargetSystemId = starSystem.Id,
            // TargetPlanetId = missionTemplate.TargetPlanet.Id,
            // TargetSystemId = missionTemplate.TargetSystem.Id,
            PirateGroupSpawnRuleConfig = missionTemplate.PirateGroupSpawnRuleConfig,
            Reward = new MissionRewardData
            {
                Credits = Random.Range(missionTemplate.CreditRewardMin, missionTemplate.CreditRewardMax + 1),
                Xp = Random.Range(missionTemplate.XpRewardMin, missionTemplate.XpRewardMax + 1)
            }
            // Objective = new MissionObjectiveData
            // {
            //     TargetEnemyId = "enemy_raider_light_01",
            //     RequiredAmount = 3,
            //     CurrentAmount = 0
            // }
        };
    }

    private MissionInstanceData BuildReconMission(StarSystemConfig starSystem, PlanetConfig planet, MissionTemplateConfig missionTemplate)
    {
        Debug.Log("[PlanetMissionOfferGenerator] starSystem = " + starSystem);
        Debug.Log("[PlanetMissionOfferGenerator] planet = " + planet);
        Debug.Log("[PlanetMissionOfferGenerator] missionTemplate = " + missionTemplate);

        return new MissionInstanceData
        {
            MissionRuntimeId = $"mission_runtime_recon_{Guid.NewGuid():N}",
            TemplateId = missionTemplate.Id,
            Title = missionTemplate.DisplayName,
            Description = missionTemplate.Description,
            MissionType = MissionType.Recon,
            Status = MissionStatus.Available,
            SourcePlanetId = planet.Id,
            SourceSystemId = starSystem == null ? "" : starSystem.Id,
            TargetPlanetId = missionTemplate.TargetPlanet == null ? "" : missionTemplate.TargetPlanet.Id,
            TargetSystemId = missionTemplate.TargetSystem.Id,
            Reward = new MissionRewardData
            {
                Credits = Random.Range(missionTemplate.CreditRewardMin, missionTemplate.CreditRewardMax + 1),
                Xp = Random.Range(missionTemplate.XpRewardMin, missionTemplate.XpRewardMax + 1)
            },
            Objective = new MissionObjectiveData
            {
                RequiredAmount = 1,
                CurrentAmount = 0
            }
        };
    }
}