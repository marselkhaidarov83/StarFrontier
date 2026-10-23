using System;
using System.Linq;

public static class DeliveryMissionCargoSelector
{
    public static bool TrySelect(DeliveryMissionTemplateConfig template, int freeVolume,
        Func<string, bool> isRegistered, double itemRoll, double amountRoll, out ItemConfig item, out int quantity)
    {
        item = null; quantity = 0;
        if (template?.CargoOptions == null || template.MinimumQuantity <= 0 || template.MaximumQuantity < template.MinimumQuantity) return false;
        var options = template.CargoOptions.Where(e => e?.Item != null && e.Weight > 0 &&
            !float.IsNaN(e.Weight) && !float.IsInfinity(e.Weight) && e.Item.CargoSize > 0 &&
            isRegistered(e.Item.Id) && freeVolume / e.Item.CargoSize >= template.MinimumQuantity).ToList();
        if (options.Count == 0) return false;
        double roll = Math.Max(0, Math.Min(1, itemRoll)) * options.Sum(e => (double)e.Weight);
        var selected = options[options.Count - 1];
        foreach (var option in options) { roll -= option.Weight; if (roll < 0) { selected = option; break; } }
        item = selected.Item;
        int max = Math.Min(template.MaximumQuantity, freeVolume / item.CargoSize);
        quantity = (int)Math.Min(max, template.MinimumQuantity +
            Math.Floor(Math.Max(0, Math.Min(1, amountRoll)) * ((long)max - template.MinimumQuantity + 1)));
        return true;
    }
}
