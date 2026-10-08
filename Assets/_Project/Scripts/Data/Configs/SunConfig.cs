using System;
using UnityEngine;

// Конфиг SunConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "SunConfig", menuName = "StarFrontier/Configs/System/Sun")]
public class SunConfig : BaseConfig
{
    [Header("Base Stats")]
    [Tooltip("Спрайт для поля sunSprite. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite sunSprite;
    [Tooltip("Визуальный размер объекта на сцене.")]
    [SerializeField] private float visualSize = 180f;
    [Tooltip("Параметр localOffset. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    public Sprite SunSprite => sunSprite;
    public float VisualSize => visualSize;
    public Vector2 LocalOffset => localOffset;
}
