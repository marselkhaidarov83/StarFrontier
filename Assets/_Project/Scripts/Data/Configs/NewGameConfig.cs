using UnityEngine;

[CreateAssetMenu(fileName = "NewGameConfig", menuName = "StarFrontier/Configs/Game/New game")]
public class NewGameConfig : BaseConfig
{
    public StarSystemConfig StartSystem;
    public AllyConfig StarterShipConfig;
    public Vector3 StartShipPosition = new Vector3(0f, -360f, -2f);
    public int StartCredit = 1000;
    public int CurrentFuel = 1000;
    public int FuelCapacity = 1000;
}
