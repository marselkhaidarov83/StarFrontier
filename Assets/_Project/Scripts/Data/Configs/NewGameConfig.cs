using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGameConfig", menuName = "StarFrontier/Configs/New game")]
public class NewGameConfig : BaseConfig
{
    public StarSystemConfig StartSystem;
    public int StartCredit = 1000;
    public int CurrentFuel = 1000;
    public int FuelCapacity = 1000;
}