using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "hull_group_", menuName = "StarFrontier/Configs/Ship/Hull Group")]
public sealed class HullGroupConfig : BaseConfig
{
    [Min(0)] public int Level = 1;
    [SerializeField] private List<HullConfig> hullConfigs = new();
    public IReadOnlyList<HullConfig> HullConfigs => hullConfigs;

    public HullConfig PreviewHull
    {
        get { List<HullConfig> candidates = Candidates(); return candidates.Count > 0 ? candidates[0] : null; }
    }

    public HullConfig Resolve(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && hullConfigs != null)
            foreach (HullConfig hull in hullConfigs)
                if (hull != null && hull.Id == id) return hull;
        return PreviewHull;
    }

    public HullConfig PickRandom()
    {
        List<HullConfig> candidates = Candidates();
        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    private List<HullConfig> Candidates()
    {
        var result = new List<HullConfig>();
        var seen = new HashSet<string>();
        if (hullConfigs != null)
            foreach (HullConfig hull in hullConfigs)
                if (hull != null && hull.Level == Level && !string.IsNullOrWhiteSpace(hull.Id) && seen.Add(hull.Id))
                    result.Add(hull);
        return result;
    }
}
