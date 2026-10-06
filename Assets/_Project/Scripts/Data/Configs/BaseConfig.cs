using UnityEngine;

public abstract class BaseConfig : ScriptableObject
{
    [Header("Common Info")]
    [Tooltip("Уникальный внутренний код этого конфига. По нему игровые системы находят нужную запись, проверяют повторы и связывают игровые объекты между собой.")]
    [SerializeField] private string id;

    [Tooltip("Название, которое показывается игроку или используется в служебных списках вместо внутреннего кода.")]
    [SerializeField] private string displayName;

    [Tooltip("Описание самого конфига: что это за запись, за какую часть игры отвечает и где её увидеть или проверить.")]
    [SerializeField][TextArea] private string description;

    [Header("Common Visuals")]
    [Tooltip("Картинка для этой записи. Используется там, где объект нужно показать в списке, подсказке или интерфейсе.")]
    [SerializeField] private Sprite icon;

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
}