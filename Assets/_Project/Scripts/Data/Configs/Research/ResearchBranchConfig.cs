using System;
using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Configs/Research/Branch")]
public sealed class ResearchBranchConfig : BaseConfig
{
    [Tooltip("Направление этой ветки для неспециализированных станций. Научная станция показывает все ветки.")]
    public StationType StationType;
    public ResearchNode[] Nodes = Array.Empty<ResearchNode>();
}
