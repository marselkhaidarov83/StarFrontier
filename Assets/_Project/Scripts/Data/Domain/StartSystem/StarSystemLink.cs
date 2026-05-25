using System;
using UnityEngine;

[Serializable]
public class StarSystemLink
{
    [Header("Target")]
    [SerializeField] private StarSystemConfig linkedSystem;

    [Header("Travel Cost")]
    [SerializeField] private int parsecDistance;

    [Header("System Travel Points")]
    [SerializeField] private Vector3 exitPoint;
    [SerializeField] private Vector3 entryPoint;

    [Header("Visual")]
    [SerializeField] private Sprite sprite;

    [Header("Size")]
    [SerializeField] private float size = 50f;

    public StarSystemConfig LinkedSystem => linkedSystem;
    public int ParsecDistance => parsecDistance;

    public Vector3 ExitPoint => exitPoint;
    public Vector3 EntryPoint => entryPoint;

    public Sprite Sprite => sprite;
    public float Size => size;
}