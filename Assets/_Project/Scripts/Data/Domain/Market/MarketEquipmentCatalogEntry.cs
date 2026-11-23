using System;
using UnityEngine;

// Общие данные каталога и профиля рынка. Вес генерации задаётся только в профиле.
[Serializable]
public class MarketEquipmentCatalogEntry
{
    public WeaponConfig Weapon;
    public ShipEquipmentConfig Component;

    public string EquipmentId => Component != null ? Component.Id : Weapon != null ? Weapon.Id : null;

    public bool Matches(SystemNpcWeaponRuntimeState item) =>
        item != null && (Component != null ? item.ComponentConfigId == Component.Id : Weapon != null && item.WeaponConfigId == Weapon.Id);

    [Tooltip("Только союзное оборудование доступно для покупки и обратного выкупа.")]
    public bool Allied = true;
    [Min(1)] public int NewPrice = 100;
}
