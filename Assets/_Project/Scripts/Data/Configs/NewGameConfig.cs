using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGameConfig", menuName = "StarFrontier/Configs/New game")]
public class NewGameConfig : BaseConfig
{
    public StarSystemConfig StartSystem;
    public int StartCredit = 1000;
}