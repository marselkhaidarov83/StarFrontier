using System;
using System.Collections.Generic;
using UnityEngine;

public enum MarketKindFilter2A { Goods, Hulls, Weapons, Components, All }
public sealed class MarketCardData2A
{
    public string Key, Id, Title, CategoryKey, CategoryTitle;
    public bool Equipment, Hull, Component;
    public Sprite Icon;
    public int Quantity, Price, CargoSize, CurrentDurability, MaximumDurability;
    public float Range;
    public int Damage, ShotCount;
    // Market offers use -1. Owned goods expose the ordinary amount separately from mission-owned cargo.
    public int TradeableQuantity = -1;
    public bool Locked => !Equipment && TradeableQuantity == 0;
    public bool Matches(MarketKindFilter2A kind, string category) =>
        (!(Equipment || Component) || Quantity > 0) &&
        (kind == MarketKindFilter2A.All ||
         (kind == MarketKindFilter2A.Goods && !Equipment && !Component) ||
         (kind == MarketKindFilter2A.Components && Component) ||
         (kind == MarketKindFilter2A.Hulls && Equipment && Hull) ||
         (kind == MarketKindFilter2A.Weapons && Equipment && !Hull && !Component)) &&
        (category == "all" || category == CategoryKey);
}
public static class MarketScreenRules2A
{
    public static (string Range, string Damage, string Shots, string Cargo) WeaponDetails(float range, int damage, int shots, int cargo) =>
        ("Дальность: " + range.ToString("0.##"), "Урон: " + damage, "Кол-во выстрелов: " + shots, "Занимает в трюме: " + cargo);

    public static void CollectAlliedWeaponTypes(IEnumerable<MarketEquipmentRule> catalog, List<WeaponType> result)
    {
        result.Clear();
        if (catalog != null)
            foreach (var rule in catalog)
                if (rule?.Weapon != null && rule.Allied && !result.Contains(rule.Weapon.WeaponType)) result.Add(rule.Weapon.WeaponType);
        result.Sort();
    }
    public static void BuildCategories(MarketKindFilter2A kind, IReadOnlyList<MarketCardData2A> offers,
        IReadOnlyList<MarketCardData2A> owned, List<KeyValuePair<string, string>> result, HashSet<string> seen, IReadOnlyList<WeaponType> alliedWeaponTypes)
    {
        result.Clear(); seen.Clear();
        result.Add(new KeyValuePair<string, string>("all", "\u0412\u0441\u0435")); seen.Add("all");
        if (kind == MarketKindFilter2A.Goods || kind == MarketKindFilter2A.All)
        { AddGoods(offers); AddGoods(owned); }
        if (kind == MarketKindFilter2A.All)
            result.Add(new KeyValuePair<string, string>("goods:" + ItemCategory.ShipEquipment, "Модули"));
        if (kind == MarketKindFilter2A.All)
            result.Add(new KeyValuePair<string, string>("equipment:hull", "\u041a\u043e\u0440\u043f\u0443\u0441"));
        if ((kind == MarketKindFilter2A.Weapons || kind == MarketKindFilter2A.All) && alliedWeaponTypes != null)
            foreach (var type in alliedWeaponTypes)
                if (seen.Add("equipment:" + type)) result.Add(new KeyValuePair<string, string>("equipment:" + type, EquipmentName(type)));
        // Hulls intentionally have only the All button. Weapon types do not depend on live stock.
        void AddGoods(IReadOnlyList<MarketCardData2A> items)
        {
            if (items == null) return;
            foreach (var item in items)
                if (item != null && !item.Equipment && item.Matches(MarketKindFilter2A.Goods, "all") && seen.Add(item.CategoryKey))
                    result.Add(new KeyValuePair<string, string>(item.CategoryKey, item.CategoryTitle));
        }
    }
    public static string GoodsName(ItemCategory type) => type switch {
        ItemCategory.Minerals => "Минералы", ItemCategory.Fuel => "Топливо", ItemCategory.Medicine => "Медицина",
        ItemCategory.Electronics => "Электроника", ItemCategory.Food => "Продовольствие", ItemCategory.ConsumerGoods => "Потребительские товары",
        ItemCategory.Industrial => "Промышленные товары", ItemCategory.Luxury => "Предметы роскоши", ItemCategory.Technology => "Технологии",
        ItemCategory.Currency => "Валюты", ItemCategory.ShipEquipment => "Модули", _ => type.ToString() };
    public static string EquipmentName(WeaponType type) => type switch {
        WeaponType.Laser => "Лазеры", WeaponType.Pulse => "Импульсное оружие", WeaponType.Plasma => "Плазменное оружие",
        WeaponType.Railgun => "Рельсотроны", WeaponType.Missile => "Ракеты", WeaponType.Disruptor => "Дезинтеграторы",
        WeaponType.Beam => "Лучевое оружие", WeaponType.Lance => "Копья", WeaponType.Orb => "Сферы",
        WeaponType.Singularity => "Сингулярности", WeaponType.Spore => "Споры", WeaponType.Acid => "Кислотное оружие",
        WeaponType.Swarm => "Рои", WeaponType.Tendril => "Щупальца", WeaponType.Burst => "Разрядное оружие", _ => type.ToString() };
    public static string Reason(string reason) => reason switch {
        "MarketChanged" => "Рынок обновился. Проверь новую цену и повтори действие.",
        "NotEnoughCredits" => "Недостаточно кредитов.", "NotEnoughCargoSpace" => "Недостаточно места в трюме.",
        "NotEnoughMarketStock" => "Товар закончился на рынке.", "NotEnoughItemsToSell" => "Предмет больше недоступен.",
        "NotEnoughCreditsOrCargo" => "Недостаточно кредитов или места в трюме.",
        "PriceNotFound" => "На этом рынке предмет не принимают.", "EquipmentUnavailable" => "Оборудование больше недоступно.",
        "MarketUnavailable" => "Рынок в этом месте недоступен.", _ => "Действие недоступно. Обнови выбор." };
}
