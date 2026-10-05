using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyNamePoolConfig",
    menuName = "StarFrontier/Configs/Npc/Enemy Name Pool")]
public sealed class EnemyNamePoolConfig : BaseConfig
{
    [Header("Owner")]
    [SerializeField]
    private WeaponGroupEnemyFaction faction =
        WeaponGroupEnemyFaction.AI;

    [Header("Names")]
    [SerializeField]
    private string[] names = new string[0];

    public WeaponGroupEnemyFaction Faction =>
        faction;

    public IReadOnlyList<string> Names =>
        names;

    public int NameCount
    {
        get
        {
            if (names == null)
                return 0;

            int count = 0;

            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(names[i]))
                    count++;
            }

            return count;
        }
    }

    public string PickName(string seed)
    {
        if (names == null || names.Length == 0)
            return string.Empty;

        List<string> validNames =
            new List<string>(names.Length);

        for (int i = 0; i < names.Length; i++)
        {
            string candidate =
                names[i];

            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            validNames.Add(candidate.Trim());
        }

        if (validNames.Count == 0)
            return string.Empty;

        int index =
            Mathf.Abs(BuildStableHash(seed)) % validNames.Count;

        return validNames[index];
    }

    private static int BuildStableHash(string value)
    {
        unchecked
        {
            int hash = 17;

            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];

            return hash;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (names == null)
            names = new string[0];

        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == null)
                names[i] = string.Empty;

            names[i] = names[i].Trim();
        }
    }
#endif
}
