using System;
using UnityEngine;

[Serializable]
public sealed class StationPaymentCost
{
    public StationPaymentResource Resource;
    [Tooltip("Астралиты всех видов оплачиваются из общего количества. Ссылка необязательна и сохранена для совместимости старых настроек.")]
    public ItemConfig Item;
    [Min(1)] public int Amount = 1;
}

[Serializable]
public sealed class StationPaymentOption
{
    [Tooltip("Постоянный уникальный ID варианта в пределах одной операции.")]
    public string Id = string.Empty;
    public StationPaymentCost[] Costs = Array.Empty<StationPaymentCost>();
    [Tooltip("Время в секундах симуляции. 0 — выполнить сразу. Не зависит от вида валюты.")]
    [Min(0)] public float DurationSeconds;

    public bool TryCreateSnapshot(StationOperationKind kind, int targetLevel,
        out StationOperationRuntimeState snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(Id) || Costs == null || Costs.Length == 0 ||
            float.IsNaN(DurationSeconds) || float.IsInfinity(DurationSeconds) || DurationSeconds < 0f)
            return false;

        bool credits = false, astralites = false, diamonds = false;
        var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var costs = new StationPaymentCostSnapshot[Costs.Length];
        for (int i = 0; i < Costs.Length; i++)
        {
            var cost = Costs[i];
            if (cost == null || cost.Amount <= 0 ||
                !Enum.IsDefined(typeof(StationPaymentResource), cost.Resource)) return false;
            string itemId = cost.Resource == StationPaymentResource.Astralites ? cost.Item?.Id : string.Empty;
            if (!keys.Add(cost.Resource + ":" + itemId)) return false;
            credits |= cost.Resource == StationPaymentResource.Credits;
            astralites |= cost.Resource == StationPaymentResource.Astralites;
            diamonds |= cost.Resource == StationPaymentResource.Diamonds;
            costs[i] = new StationPaymentCostSnapshot
            { Resource = cost.Resource, ItemConfigId = itemId ?? string.Empty, Amount = cost.Amount };
        }
        if (diamonds ? credits || astralites : !credits || !astralites) return false;

        snapshot = new StationOperationRuntimeState
        {
            OperationId = Guid.NewGuid().ToString("N"), Kind = kind, TargetLevel = targetLevel,
            PaymentOptionId = Id, PaidCosts = costs, DurationSeconds = DurationSeconds
        };
        return true;
    }
}

[Serializable]
public sealed class StationUpgradeSettings
{
    [Range(2, 10)] public int TargetLevel = 2;
    public StationPaymentOption[] PaymentOptions = Array.Empty<StationPaymentOption>();
}
