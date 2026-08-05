using UnityEngine;

public enum AllyBehaviourScenario
{
    [InspectorName("normal")]
    Normal = 0,

    [InspectorName("enemy_invasion")]
    EnemyInvasion = 10,

    [InspectorName("enemy_system_invasion")]
    EnemySystemInvasion = 20
}