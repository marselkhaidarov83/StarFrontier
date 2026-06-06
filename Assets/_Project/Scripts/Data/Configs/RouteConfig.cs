using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "RouteConfig", menuName = "StarFrontier/Configs/Route")]
public class RouteConfig: BaseConfig
{
    [SerializeField] public StarSystemConfig FromSystem;
    [SerializeField] public StarSystemConfig ToSystem;

    [SerializeField] public bool IsLockedAtStart;
    [SerializeField] public int RequiredScanLevel;
    [SerializeField] public int ParsecDistance;
}