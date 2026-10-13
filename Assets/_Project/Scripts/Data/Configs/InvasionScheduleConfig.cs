using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "invasion_schedule_",
    menuName = "StarFrontier/Configs/War/Invasion Schedule")]
public sealed class InvasionScheduleConfig : BaseConfig
{
    [Header("Level Rules")]
    [Tooltip("Настройки автоматических вторжений по уровню игры.")]
    [SerializeField] private InvasionScheduleLevelEntry[] levelEntries =
        new InvasionScheduleLevelEntry[0];

    public InvasionScheduleLevelEntry GetEntryForGalaxyLevel(int galaxyLevel)
    {
        int clampedLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

        if (levelEntries == null)
            return null;

        for (int i = 0; i < levelEntries.Length; i++)
        {
            InvasionScheduleLevelEntry entry =
                levelEntries[i];

            if (entry == null)
                continue;

            if (entry.GalaxyLevel == clampedLevel)
                return entry;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelEntries == null)
            levelEntries = new InvasionScheduleLevelEntry[0];

        for (int i = 0; i < levelEntries.Length; i++)
        {
            if (levelEntries[i] == null)
                levelEntries[i] = new InvasionScheduleLevelEntry();

            levelEntries[i].Validate();
        }
    }
#endif
}

[Serializable]
public sealed class InvasionScheduleLevelEntry
{
    [Tooltip("Уровень игры / галактики.")]
    [SerializeField, Range(1, 10)] private int galaxyLevel = 1;

    [Tooltip("Максимальное количество активных вторжений одновременно.")]
    [SerializeField, Min(0)] private int maxActiveInvasions = 1;

    [Tooltip("Период между попытками создать новое вторжение, в секундах игрового времени.")]
    [SerializeField, Min(1f)] private float invasionPeriodSeconds = 180f;

    public int GalaxyLevel => galaxyLevel;
    public int MaxActiveInvasions => maxActiveInvasions;
    public float InvasionPeriodSeconds => invasionPeriodSeconds;

#if UNITY_EDITOR
    public void Validate()
    {
        galaxyLevel = Mathf.Clamp(galaxyLevel, 1, 10);
        maxActiveInvasions = Mathf.Max(0, maxActiveInvasions);
        invasionPeriodSeconds = Mathf.Max(1f, invasionPeriodSeconds);
    }
#endif
}