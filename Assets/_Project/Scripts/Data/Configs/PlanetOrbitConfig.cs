using UnityEngine;

// Конфиг PlanetOrbitConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "PlanetOrbitConfig", menuName = "StarFrontier/Configs/System/Planet Orbit")]
public class PlanetOrbitConfig : BaseConfig
{
    [Header("Base Stats")]
    [Tooltip("Радиус орбиты объекта вокруг центра.")]
    [SerializeField] private float orbitRadius = 200f;
    [Tooltip("Начальный угол объекта на орбите в градусах.")]
    [SerializeField] private float startAngleDeg = 0f;
    [Tooltip("Скорость движения по орбите в градусах за секунду.")]
    [SerializeField] private float orbitSpeedDegPerSec = 10f;
    [Tooltip("Параметр orbitCenterOffset. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector3 orbitCenterOffset = new Vector3(0, 0, -2);
    [Tooltip("Направление движения по орбите.")]
    [SerializeField] private int direction = 1;

    [Header("Orbit Visual")]
    [Tooltip("Количество точек визуальной орбиты.")]
    [SerializeField] private int orbitDotCount = 96;

    public float OrbitRadius => orbitRadius;
    public float StartAngleDeg => startAngleDeg;
    public float OrbitSpeedDegPerSec => orbitSpeedDegPerSec;
    public Vector3 OrbitCenterOffset => orbitCenterOffset;
    public int Direction => direction;

    public int OrbitDotCount => orbitDotCount;
}
