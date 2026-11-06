using System;
using UnityEngine;

[Serializable] public sealed class ResearchDependency
{
    public ResearchUpgrade Upgrade;
    [Range(1, 10)] public int RequiredLevel = 1;
}
[Serializable] public sealed class ResearchNodeLevel
{
    [Range(1, 10)] public int Level = 1;
    [Range(1, 10)] public int RequiredGalaxyLevel = 1;
    [Tooltip("Требуемые уровни исследований для запуска именно этого уровня.")]
    public ResearchDependency[] Dependencies = Array.Empty<ResearchDependency>();
    public StationPaymentOption[] PaymentOptions = Array.Empty<StationPaymentOption>();
}
[Serializable] public sealed class ResearchNode
{
    public ResearchUpgrade Upgrade;
    public string Title;
    public Sprite Icon;
    [Range(1, 10)] public int RequiredGalaxyLevel = 1;
    public Vector2 TreePosition;
    public ResearchNodeLevel[] Levels = Array.Empty<ResearchNodeLevel>();
}
[CreateAssetMenu(menuName = "StarFrontier/Configs/Research/Tree")]
public sealed class ResearchTreeConfig : BaseConfig
{
    public ResearchBranchConfig[] Branches = Array.Empty<ResearchBranchConfig>();
}
