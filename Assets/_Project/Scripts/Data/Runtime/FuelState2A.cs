using System;

[Serializable]
public sealed class FuelState2A
{
    // Значение -1 означает старое сохранение,
    // для которого Fuel ещё не инициализирован.
    public float CurrentFuel = -1f;
}