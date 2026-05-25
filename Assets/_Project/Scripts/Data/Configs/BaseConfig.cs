using UnityEngine;

public abstract class BaseConfig : ScriptableObject
{
    [Header("Common Info")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] [TextArea] private string description;
    
    [Header("Common Visuals")]
    [SerializeField] private Sprite icon;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
}