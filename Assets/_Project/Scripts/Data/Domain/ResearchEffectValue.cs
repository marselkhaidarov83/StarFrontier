using System;
using UnityEngine;

// Значение эффекта на конкретном уровне. Применение новых эффектов подключается отдельно.
[Serializable]
public sealed class ResearchEffectValue
{
    public string Parameter;
    public float Value;
    public string Unit;
    [TextArea] public string Details;
}
