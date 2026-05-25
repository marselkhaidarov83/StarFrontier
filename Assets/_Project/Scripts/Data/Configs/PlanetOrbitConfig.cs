using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlanetOrbitConfig", menuName = "StarFrontier/Configs/PlanetOrbit")]
public class PlanetOrbitConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private float orbitRadius = 200f;
    [SerializeField] private float startAngleDeg = 0f;
    [SerializeField] private float orbitSpeedDegPerSec = 10f;
    [SerializeField] private Vector3 orbitCenterOffset = new Vector3(0, 0, -2);
    [SerializeField] private float planetVisualSize = 96f;
    [SerializeField] private int direction = 1;

    public float OrbitRadius => orbitRadius;
    public float StartAngleDeg => startAngleDeg;
    public float OrbitSpeedDegPerSec => orbitSpeedDegPerSec;
    public Vector3 OrbitCenterOffset => orbitCenterOffset;
    public float PlanetVisualSize => planetVisualSize;
    public int Direction => direction;
}