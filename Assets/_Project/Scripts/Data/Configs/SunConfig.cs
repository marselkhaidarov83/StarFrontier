using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SunConfig", menuName = "StarFrontier/Configs/Sun")]
public class SunConfig : BaseConfig
{
    [Header("Base Stats")]
    [SerializeField] private Sprite sunSprite;
    [SerializeField] private float visualSize = 180f;
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    public Sprite SunSprite => sunSprite;
    public float VisualSize => visualSize;
    public Vector2 LocalOffset => localOffset;
}